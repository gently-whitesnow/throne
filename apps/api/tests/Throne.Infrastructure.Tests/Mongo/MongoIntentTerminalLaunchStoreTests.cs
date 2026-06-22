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
    private static readonly string[] IntentDream = ["intent", "dream"];
    private static readonly string[] IntentOnly = ["intent"];

    [Fact(DisplayName = "SaveAsync вне UoW апсертит ось; GetAsync читает её обратно")]
    public async Task Save_outside_uow_persists_and_reads_back()
    {
        var (_, store) = await NewScopeAsync();

        await store.SaveAsync("i-1", new TerminalLaunchRecord("work", "claude", "opus", "high", Array.Empty<string>()), CancellationToken.None);

        var loaded = await store.GetAsync("i-1", CancellationToken.None);
        loaded.Should().Be(new TerminalLaunchRecord("work", "claude", "opus", "high", Array.Empty<string>()));
    }

    [Fact(DisplayName = "Повторный SaveAsync перезаписывает последний выбор (один документ на интент)")]
    public async Task Save_is_idempotent_upsert()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-2", new TerminalLaunchRecord("interview", "claude", "opus", "high", Array.Empty<string>()), CancellationToken.None);
        await store.SaveAsync("i-2", new TerminalLaunchRecord("review", "codex", "gpt-5.5", "low", Array.Empty<string>()), CancellationToken.None);

        (await store.GetAsync("i-2", CancellationToken.None))
            .Should().Be(new TerminalLaunchRecord("review", "codex", "gpt-5.5", "low", Array.Empty<string>()));
        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "i-2"), cancellationToken: CancellationToken.None);
        count.Should().Be(1);
    }

    [Fact(DisplayName = "Effort=null у вендора без оси усилия не пишется и читается как null")]
    public async Task Null_effort_round_trips()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-3", new TerminalLaunchRecord("work", "opencode", "throne-local/x", null, Array.Empty<string>()), CancellationToken.None);

        (await store.GetAsync("i-3", CancellationToken.None)).Should().Be(
            new TerminalLaunchRecord("work", "opencode", "throne-local/x", null, Array.Empty<string>()));
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
            .Should().Be(new TerminalLaunchRecord("work", "claude", "sonnet", "medium", Array.Empty<string>()));
    }

    [Fact(DisplayName = "GetAsync на не запускавшийся интент → null")]
    public async Task Get_missing_returns_null()
    {
        var (_, store) = await NewScopeAsync();

        (await store.GetAsync("i-missing", CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync не трогает mode/vendor/model/effort и пишет union")]
    public async Task SetAttachedSkillIds_preserves_launch_axis()
    {
        var (db, store) = await NewScopeAsync();
        await store.SaveAsync(
            "i-attach",
            new TerminalLaunchRecord("work", "claude", "opus", "high", Array.Empty<string>()),
            CancellationToken.None);

        await store.SetAttachedSkillIdsAsync(
            "i-attach",
            IntentDream,
            CancellationToken.None);

        var loaded = await store.GetAsync("i-attach", CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Mode.Should().Be("work");
        loaded.Vendor.Should().Be("claude");
        loaded.Model.Should().Be("opus");
        loaded.Effort.Should().Be("high");
        loaded.AttachedSkillIds.Should().BeEquivalentTo(IntentDream);
    }

    [Fact(DisplayName = "SaveAsync после hot-attach не сбрасывает attached_skill_ids")]
    public async Task Save_after_attach_keeps_attached_skill_ids()
    {
        var (_, store) = await NewScopeAsync();
        await store.SaveAsync(
            "i-keep",
            new TerminalLaunchRecord("work", "claude", "opus", "high", Array.Empty<string>()),
            CancellationToken.None);
        await store.SetAttachedSkillIdsAsync("i-keep", IntentOnly, CancellationToken.None);

        // Restart-style overwrite: new launch axis, no attached_skill_ids change requested.
        await store.SaveAsync(
            "i-keep",
            new TerminalLaunchRecord("review", "claude", "sonnet", "medium", Array.Empty<string>()),
            CancellationToken.None);

        var loaded = await store.GetAsync("i-keep", CancellationToken.None);
        loaded!.Mode.Should().Be("review");
        loaded.Model.Should().Be("sonnet");
        loaded.AttachedSkillIds.Should().BeEquivalentTo(IntentOnly);
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync с пустым списком убирает поле")]
    public async Task SetAttachedSkillIds_empty_unsets_field()
    {
        var (db, store) = await NewScopeAsync();
        await store.SaveAsync(
            "i-clear",
            new TerminalLaunchRecord("work", "claude", "opus", "high", Array.Empty<string>()),
            CancellationToken.None);
        await store.SetAttachedSkillIdsAsync("i-clear", IntentOnly, CancellationToken.None);

        await store.SetAttachedSkillIdsAsync("i-clear", Array.Empty<string>(), CancellationToken.None);

        var raw = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "i-clear")).FirstAsync(CancellationToken.None);
        raw.Contains("attached_skill_ids").Should().BeFalse();
        var loaded = await store.GetAsync("i-clear", CancellationToken.None);
        loaded!.AttachedSkillIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "Tolerant read: документ без attached_skill_ids читается как пустой список")]
    public async Task Tolerant_read_legacy_doc_returns_empty_attached()
    {
        var (db, store) = await NewScopeAsync();
        await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches).InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = "i-legacy-attach",
                ["mode"] = "work",
                ["vendor"] = "claude",
                ["model"] = "sonnet",
                ["effort"] = "medium",
            },
            cancellationToken: CancellationToken.None);

        var loaded = await store.GetAsync("i-legacy-attach", CancellationToken.None);
        loaded!.AttachedSkillIds.Should().NotBeNull();
        loaded.AttachedSkillIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync с upsert=false: на несуществующий интент → no-op")]
    public async Task SetAttachedSkillIds_missing_doc_is_noop()
    {
        var (db, store) = await NewScopeAsync();

        await store.SetAttachedSkillIdsAsync("i-ghost", IntentOnly, CancellationToken.None);

        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "i-ghost"),
                cancellationToken: CancellationToken.None);
        count.Should().Be(0);
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
