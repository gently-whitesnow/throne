using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
public class MongoIntentRepositoryTests(MongoFixture fixture)
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "CreateAsync пишет Intent в intents и v1 snapshot в text_versions")]
    public async Task Create_persists_canonical_and_v1_snapshot()
    {
        var (db, repo, uow) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "hello world", ["throne"], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "hello world", Now, TextVersionAuthor.Agent);

        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, ct), CancellationToken.None);

        var stored = await db.GetCollection<IntentDocument>(MongoCollectionNames.Intents)
            .Find(x => x.Id == id.Value).FirstOrDefaultAsync();
        stored.Should().NotBeNull();
        stored!.Text.Should().Be("hello world");
        stored.CurrentVersion.Should().Be(1);
        stored.Tags.Should().Equal("throne");

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
        var intent = Intent.Create(id, "body", ["a", "b"], Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value,
            "body", Now, TextVersionAuthor.Agent);
        await uow.ExecuteAsync(ct => repo.CreateAsync(intent, version, ct), CancellationToken.None);

        var fetched = await repo.GetByIdAsync(id, CancellationToken.None);

        fetched.Should().NotBeNull();
        fetched!.Text.Should().Be("body");
        fetched.CurrentVersion.Should().Be(1);
        fetched.Tags.Should().Equal("a", "b");
    }

    [Fact(DisplayName = "GetByIdAsync возвращает null для несуществующего id")]
    public async Task Get_returns_null_when_missing()
    {
        var (_, repo, _) = await NewScopeAsync();

        var fetched = await repo.GetByIdAsync(new IntentId("nope"), CancellationToken.None);

        fetched.Should().BeNull();
    }

    [Fact(DisplayName = "CreateAsync вне UoW бросает InvalidOperationException")]
    public async Task Create_without_uow_throws()
    {
        var (_, repo, _) = await NewScopeAsync();

        var id = IntentId.New();
        var intent = Intent.Create(id, "x", null, Now);
        var version = TextVersion.CreateSnapshot(
            Guid.NewGuid().ToString("N"), TextVersionOwnerKind.Intent, id.Value, "x", Now, TextVersionAuthor.Agent);

        var act = () => repo.CreateAsync(intent, version, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private async Task<(IMongoDatabase Db, MongoIntentRepository Repo, IUnitOfWork Uow)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        var repo = new MongoIntentRepository(db, sessions);
        var uow = new MongoUnitOfWork(fixture.Client, sessions);
        return (db, repo, uow);
    }
}

[CollectionDefinition(nameof(MongoIntegrationFixture))]
public sealed class MongoIntegrationFixture : ICollectionFixture<MongoFixture>
{
}
