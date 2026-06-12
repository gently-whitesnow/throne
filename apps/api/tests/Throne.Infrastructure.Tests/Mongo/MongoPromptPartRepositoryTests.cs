using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoPromptPartRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет part + v1 TextVersion транзакционно (owner=prompt_part)")]
    public async Task Create_persists_part_and_v1_snapshot()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var part = MakePart("work", "work text");
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"),
            TextVersionOwnerKind.PromptPart,
            part.Id.Value,
            part.Text,
            Now,
            TextVersionAuthor.System);

        var outcome = await uow.ExecuteAsync(ct => repo.CreateAsync(part, version, ct), CancellationToken.None);
        outcome.Should().BeOfType<CreatePromptPartOutcome.Created>();

        var stored = await db.GetCollection<PromptPartDocument>(MongoCollectionNames.PromptParts)
            .Find(x => x.Id == part.Id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Scope.Should().Be(PromptPartScopeNames.User);
        stored.Key.Should().Be("work");
        stored.Text.Should().Be("work text");
        stored.CurrentVersion.Should().Be(1);

        var versions = await db.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions)
            .Find(x => x.OwnerId == part.Id.Value).ToListAsync();
        versions.Should().ContainSingle();
        versions[0].OwnerKind.Should().Be("prompt_part");
        versions[0].Kind.Should().Be("create");
        versions[0].Snapshot.Should().Be("work text");
    }

    [Fact(DisplayName = "GetByScopeKeyAsync возвращает part по (scope, key)")]
    public async Task GetByScopeKey_returns_part()
    {
        var (_, repo, uow) = await NewScopeAsync();
        var part = MakePart("work", "work text");
        await uow.ExecuteAsync(ct => repo.CreateAsync(part, MakeV1(part), ct), CancellationToken.None);

        var loaded = await repo.GetByScopeKeyAsync(PromptPartScopeNames.User, "work", CancellationToken.None);

        loaded.Should().NotBeNull();
        loaded!.Id.Value.Should().Be(part.Id.Value);
        loaded.Scope.Should().Be(PromptPartScopeNames.User);
        loaded.Key.Should().Be("work");
        loaded.Text.Should().Be("work text");

        var missing = await repo.GetByScopeKeyAsync(PromptPartScopeNames.User, "absent", CancellationToken.None);
        missing.Should().BeNull();
    }

    [Fact(DisplayName = "ReplaceTextAsync поднимает версию и дописывает TextVersion (owner=prompt_part)")]
    public async Task ReplaceText_bumps_version_and_appends_text_version()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var part = MakePart("work", "work text");
        await uow.ExecuteAsync(ct => repo.CreateAsync(part, MakeV1(part), ct), CancellationToken.None);

        var outcome = await uow.ExecuteAsync(
            ct => repo.ReplaceTextAsync(
                part.Id,
                expectedVersion: 1,
                oldText: "work",
                newText: "edited",
                changedBy: TextVersionAuthor.System,
                now: Now,
                ct),
            CancellationToken.None);

        outcome.Should().BeOfType<ReplacePromptPartTextOutcome.Replaced>();
        var replaced = (ReplacePromptPartTextOutcome.Replaced)outcome;
        replaced.Part.Text.Should().Be("edited text");
        replaced.Part.CurrentVersion.Should().Be(2);

        var stored = await db.GetCollection<PromptPartDocument>(MongoCollectionNames.PromptParts)
            .Find(x => x.Id == part.Id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Text.Should().Be("edited text");
        stored.CurrentVersion.Should().Be(2);

        var versions = await db.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions)
            .Find(x => x.OwnerId == part.Id.Value)
            .SortBy(x => x.Version)
            .ToListAsync();
        versions.Should().HaveCount(2);
        versions[1].OwnerKind.Should().Be("prompt_part");
        versions[1].Version.Should().Be(2);
        versions[1].Kind.Should().Be("replace");
        versions[1].OldText.Should().Be("work");
        versions[1].NewText.Should().Be("edited");
    }

    private static PromptPart MakePart(string key, string text) =>
        PromptPart.Create(
            PromptPartId.New(),
            PromptPartScopeNames.User,
            key,
            text,
            description: null,
            modeRoles: [],
            Now);

    private static TextVersion MakeV1(PromptPart part) =>
        TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"),
            TextVersionOwnerKind.PromptPart,
            part.Id.Value,
            part.Text,
            Now,
            TextVersionAuthor.System);

    private async Task<(IMongoDatabase Db, MongoPromptPartRepository Repo, MongoUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_prompt_part_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoPromptPartRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (db, repo, uow);
    }
}
