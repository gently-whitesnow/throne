using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет Intent в intents и v1 snapshot в text_versions")]
    public async Task Create_persists_canonical_and_v1_snapshot()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var tagId = TagId.New();
        var intent = Intent.Create(id, "user-1", "hello world", [tagId], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "hello world", Now, TextVersionAuthor.Agent);

        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(x => x.Id == id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Text.Should().Be("hello world");
        stored.Status.Should().Be(IntentStatusNames.Draft);
        stored.CurrentVersion.Should().Be(1);
        stored.TagIds.Should().Equal(tagId.Value);

        var versions = await db.GetCollection<TextVersionDocument>(MongoCollectionNames.TextVersions)
            .Find(x => x.OwnerId == id.Value).ToListAsync();
        versions.Should().HaveCount(1);
        versions[0].Version.Should().Be(1);
        versions[0].Kind.Should().Be("create");
        versions[0].OwnerKind.Should().Be("intent");
        versions[0].Snapshot.Should().Be("hello world");
    }

    [Fact(DisplayName = "GetByIdAsync читает Intent из intents в доменной форме")]
    public async Task Get_returns_persisted_intent()
    {
        var (_, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var a = TagId.New();
        var b = TagId.New();
        var intent = Intent.Create(id, "user-1", "body", [a, b], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var fetched = await repo.GetByIdAsync(id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.Text.Should().Be("body");
        fetched.Status.Should().Be(IntentStatusNames.Draft);
        fetched.CurrentVersion.Should().Be(1);
        fetched.TagIds.Should().Equal(a, b);
    }

    [Fact(DisplayName = "GetByIdAsync возвращает null для несуществующего id")]
    public async Task Get_returns_null_when_missing()
    {
        var (_, repo, _) = await NewScopeAsync();

        var fetched = await repo.GetByIdAsync(new IntentId("nope"), CancellationToken.None);

        fetched.Should().BeNull();
    }

    [Fact(DisplayName = "SetStatusAsync обновляет статус и пишет status-change log")]
    public async Task SetStatus_updates_status_and_writes_log()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "user-1", "body", [TagId.New()], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), ct), CancellationToken.None);

        var outcome = await uow.ExecuteAsync(
            ct => repo.SetStatusAsync(
                id,
                IntentStatusNames.Work,
                appendText: null,
                IntentTrainingAuthor.System,
                "get_instruction_bundle:work",
                Now.AddMinutes(5),
                ct),
            CancellationToken.None);

        var updated = outcome.Should().BeOfType<SetIntentStatusOutcome.Updated>().Subject.Intent;
        updated.Status.Should().Be(IntentStatusNames.Work);
        updated.CurrentVersion.Should().Be(1);

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(x => x.Id == id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Status.Should().Be(IntentStatusNames.Work);

        var changes = await db.GetCollection<IntentStatusChangeDocument>(MongoCollectionNames.IntentStatusChanges)
            .Find(x => x.IntentId == id.Value)
            .SortBy(x => x.CreatedAt)
            .ToListAsync();
        changes.Should().HaveCount(2);
        changes[1].FromStatus.Should().Be(IntentStatusNames.Draft);
        changes[1].ToStatus.Should().Be(IntentStatusNames.Work);
        changes[1].Source.Should().Be("get_instruction_bundle:work");
        changes[1].CreatedBy.Should().Be("system");
    }

    [Fact(DisplayName = "CreateAsync вне UoW бросает InvalidOperationException")]
    public async Task Create_without_uow_throws()
    {
        var (_, repo, _) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "user-1", "x", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "x", Now, TextVersionAuthor.Agent);

        var act = () => repo.CreateAsync(intent, version, InitialStatusChange(intent), Array.Empty<Tag>(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task<(IMongoDatabase Db, MongoIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepository(db, sessions, new TestCurrentUserAccessor());
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (db, repo, uow);
    }

    private static IntentStatusChange InitialStatusChange(Intent intent) =>
        IntentStatusChange.Create(
            Guid.NewGuid().ToString("N"),
            intent.Id,
            intent.CurrentVersion,
            intent.Status,
            intent.Status,
            "test:create",
            Now,
            IntentTrainingAuthor.Agent);
}

[CollectionDefinition(nameof(MongoIntegrationFixture))]
public sealed class MongoIntegrationFixture : ICollectionFixture<MongoFixture>
{
}
