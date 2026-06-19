using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Terminals;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoIntentTerminalLaunchStoreTests(MongoFixture fixture)
{
    [Fact(DisplayName = "SaveAsync вне UoW апсертит ось; GetAsync читает её обратно")]
    public async Task Save_outside_uow_persists_and_reads_back()
    {
        var (_, store) = await NewScopeAsync();

        await store.SaveAsync("i-1", new TerminalLaunchRecord("work", "claude", "opus", "high"), CancellationToken.None);

        var loaded = await store.GetAsync("i-1", CancellationToken.None);
        loaded.Should().Be(new TerminalLaunchRecord("work", "claude", "opus", "high"));
    }

    [Fact(DisplayName = "Повторный SaveAsync перезаписывает последний выбор (один документ на интент)")]
    public async Task Save_is_idempotent_upsert()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-2", new TerminalLaunchRecord("interview", "claude", "opus", "high"), CancellationToken.None);
        await store.SaveAsync("i-2", new TerminalLaunchRecord("review", "codex", "gpt-5.5", "low"), CancellationToken.None);

        (await store.GetAsync("i-2", CancellationToken.None))
            .Should().Be(new TerminalLaunchRecord("review", "codex", "gpt-5.5", "low"));
        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "i-2"), cancellationToken: CancellationToken.None);
        count.Should().Be(1);
    }

    [Fact(DisplayName = "Effort=null у вендора без оси усилия не пишется и читается как null")]
    public async Task Null_effort_round_trips()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-3", new TerminalLaunchRecord("work", "opencode", "throne-local/x", null), CancellationToken.None);

        (await store.GetAsync("i-3", CancellationToken.None)).Should().Be(
            new TerminalLaunchRecord("work", "opencode", "throne-local/x", null));
        var raw = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "i-3")).FirstAsync(CancellationToken.None);
        raw.Contains("effort").Should().BeFalse();
    }

    [Fact(DisplayName = "Tolerant read: лишние persisted-поля не валят десериализацию")]
    public async Task Tolerant_read_ignores_unknown_fields()
    {
        var (db, store) = await NewScopeAsync();
        await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches).InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = "i-legacy",
                ["mode"] = "work",
                ["vendor"] = "claude",
                ["model"] = "sonnet",
                ["effort"] = "medium",
                ["legacy_started_at"] = "2026-01-01",
            },
            cancellationToken: CancellationToken.None);

        (await store.GetAsync("i-legacy", CancellationToken.None))
            .Should().Be(new TerminalLaunchRecord("work", "claude", "sonnet", "medium"));
    }

    [Fact(DisplayName = "GetAsync на не запускавшийся интент → null")]
    public async Task Get_missing_returns_null()
    {
        var (_, store) = await NewScopeAsync();

        (await store.GetAsync("i-missing", CancellationToken.None)).Should().BeNull();
    }

    private async Task<(IMongoDatabase Db, MongoIntentTerminalLaunchStore Store)> NewScopeAsync()
    {
        var name = $"throne_test_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var store = new MongoIntentTerminalLaunchStore(db, new MongoSessionAccessor());
        return (db, store);
    }
}
