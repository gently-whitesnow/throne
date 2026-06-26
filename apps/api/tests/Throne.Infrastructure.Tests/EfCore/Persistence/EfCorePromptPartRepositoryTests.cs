using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.EfCore;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCorePromptPartRepositoryTests(SqliteFixture fixture)
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

        var stored = await FindPartAsync(db, part.Id.Value);
        stored.Should().NotBeNull();
        stored!.Scope.Should().Be(PromptPartScopeNames.User);
        stored.Key.Should().Be("work");
        stored.Text.Should().Be("work text");
        stored.CurrentVersion.Should().Be(1);

        var versions = await ListVersionsAsync(db, part.Id.Value);
        versions.Should().ContainSingle();
        versions[0].OwnerKind.Should().Be("prompt_part");
        versions[0].Kind.Should().Be("create");
        versions[0].Snapshot.Should().Be("work text");
    }

    [Fact(DisplayName = "CreateAsync duplicate key не оставляет orphan TextVersion")]
    public async Task Create_duplicate_key_does_not_persist_orphan_version()
    {
        var (db, repo, uow) = await NewScopeAsync();
        var first = MakePart("work", "work text");
        var duplicate = PromptPart.Create(
            PromptPartId.New(),
            PromptPartScopeNames.User,
            "work",
            "other text",
            description: null,
            modeRoles: [],
            Now);
        await uow.ExecuteAsync(ct => repo.CreateAsync(first, MakeV1(first), ct), CancellationToken.None);

        var outcome = await uow.ExecuteAsync(
            ct => repo.CreateAsync(duplicate, MakeV1(duplicate), ct),
            CancellationToken.None);

        outcome.Should().BeOfType<CreatePromptPartOutcome.KeyConflict>();
        var duplicateVersions = await ListVersionsAsync(db, duplicate.Id.Value);
        duplicateVersions.Should().BeEmpty();
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

        var stored = await FindPartAsync(db, part.Id.Value);
        stored.Should().NotBeNull();
        stored!.Text.Should().Be("edited text");
        stored.CurrentVersion.Should().Be(2);

        var versions = await ListVersionsAsync(db, part.Id.Value);
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

    private async Task<(SqliteTestDatabase Db, EfPromptPartRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<EfPromptPartRepository>(), db.GetRequiredService<IUnitOfWork>());
    }

    private static async Task<PromptPartRow?> FindPartAsync(SqliteTestDatabase db, string id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<PromptPartRow>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    private static async Task<List<TextVersionRow>> ListVersionsAsync(SqliteTestDatabase db, string ownerId)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<TextVersionRow>().AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderBy(x => x.Version)
            .ToListAsync();
    }
}
