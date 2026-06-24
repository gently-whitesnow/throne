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
    private const string Work = "work";
    private const string Review = "review";

    private static readonly string[] IntentDream = ["intent", "dream"];
    private static readonly string[] IntentOnly = ["intent"];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptySelections =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    [Fact(DisplayName = "SaveAsync вне UoW апсертит ось; GetAsync читает её обратно")]
    public async Task Save_outside_uow_persists_and_reads_back()
    {
        var (_, store) = await NewScopeAsync();

        await store.SaveAsync("i-1", Launch("work", "claude", "opus", "high"), CancellationToken.None);

        var loaded = await store.GetAsync("i-1", CancellationToken.None);
        loaded.Should().NotBeNull();
        AssertAxis(loaded!, "work", "claude", "opus", "high");
        loaded!.AttachedSkillIds.Should().BeEmpty();
        loaded.SelectedSkillIdsByMode.Should().BeEmpty();
    }

    [Fact(DisplayName = "Повторный SaveAsync перезаписывает последний выбор (один документ на интент)")]
    public async Task Save_is_idempotent_upsert()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-2", Launch("interview", "claude", "opus", "high"), CancellationToken.None);
        await store.SaveAsync("i-2", Launch("review", "codex", "gpt-5.5", "low"), CancellationToken.None);

        var loaded = await store.GetAsync("i-2", CancellationToken.None);
        AssertAxis(loaded!, "review", "codex", "gpt-5.5", "low");
        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "i-2"), cancellationToken: CancellationToken.None);
        count.Should().Be(1);
    }

    [Fact(DisplayName = "Effort=null у вендора без оси усилия не пишется и читается как null")]
    public async Task Null_effort_round_trips()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-3", Launch("work", "opencode", "throne-local/x", null), CancellationToken.None);

        var loaded = await store.GetAsync("i-3", CancellationToken.None);
        AssertAxis(loaded!, "work", "opencode", "throne-local/x", null);
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

        var loaded = await store.GetAsync("i-legacy", CancellationToken.None);
        AssertAxis(loaded!, "work", "claude", "sonnet", "medium");
    }

    [Fact(DisplayName = "GetAsync на не запускавшийся интент → null")]
    public async Task Get_missing_returns_null()
    {
        var (_, store) = await NewScopeAsync();

        (await store.GetAsync("i-missing", CancellationToken.None)).Should().BeNull();
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync не трогает mode/vendor/model/effort и пишет union в выбор режима")]
    public async Task SetAttachedSkillIds_preserves_launch_axis_and_unions_into_mode_selection()
    {
        var (_, store) = await NewScopeAsync();
        await store.SaveAsync("i-attach", Launch("work", "claude", "opus", "high"), CancellationToken.None);

        await store.SetAttachedSkillIdsAsync("i-attach", Work, IntentDream, CancellationToken.None);

        var loaded = await store.GetAsync("i-attach", CancellationToken.None);
        AssertAxis(loaded!, "work", "claude", "opus", "high");
        loaded!.AttachedSkillIds.Should().BeEquivalentTo(IntentDream);
        loaded.SelectedSkillIdsByMode.Should().ContainKey(Work);
        loaded.SelectedSkillIdsByMode[Work].Should().BeEquivalentTo(IntentDream);
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync делает $addToSet — не дублирует существующий выбор")]
    public async Task SetAttachedSkillIds_addToSet_does_not_duplicate()
    {
        var (_, store) = await NewScopeAsync();
        await store.SaveAsync("i-union", Launch("work", "claude", "opus", "high"), CancellationToken.None);
        await store.SaveSelectedSkillIdsAsync("i-union", Work, IntentOnly, CancellationToken.None);

        await store.SetAttachedSkillIdsAsync("i-union", Work, IntentDream, CancellationToken.None);

        var loaded = await store.GetAsync("i-union", CancellationToken.None);
        loaded!.SelectedSkillIdsByMode[Work].Should().BeEquivalentTo(IntentDream);
    }

    [Fact(DisplayName = "SaveSelectedSkillIdsAsync хранит выбор раздельно по режимам и перезаписывает свой режим")]
    public async Task SaveSelectedSkillIds_per_mode_and_replaces_own_mode()
    {
        var (_, store) = await NewScopeAsync();
        await store.SaveAsync("i-modes", Launch("work", "claude", "opus", "high"), CancellationToken.None);

        await store.SaveSelectedSkillIdsAsync("i-modes", Work, IntentDream, CancellationToken.None);
        await store.SaveSelectedSkillIdsAsync("i-modes", Review, IntentOnly, CancellationToken.None);
        await store.SaveSelectedSkillIdsAsync("i-modes", Work, IntentOnly, CancellationToken.None);

        var loaded = await store.GetAsync("i-modes", CancellationToken.None);
        loaded!.SelectedSkillIdsByMode[Work].Should().BeEquivalentTo(IntentOnly);
        loaded.SelectedSkillIdsByMode[Review].Should().BeEquivalentTo(IntentOnly);
    }

    [Fact(DisplayName = "SaveAsync после hot-attach не сбрасывает attached_skill_ids и выбор по режимам")]
    public async Task Save_after_attach_keeps_runtime_and_selection()
    {
        var (_, store) = await NewScopeAsync();
        await store.SaveAsync("i-keep", Launch("work", "claude", "opus", "high"), CancellationToken.None);
        await store.SetAttachedSkillIdsAsync("i-keep", Work, IntentOnly, CancellationToken.None);

        // Restart-style overwrite: new launch axis, no attached/selection change requested.
        await store.SaveAsync("i-keep", Launch("review", "claude", "sonnet", "medium"), CancellationToken.None);

        var loaded = await store.GetAsync("i-keep", CancellationToken.None);
        AssertAxis(loaded!, "review", "claude", "sonnet", "medium");
        loaded!.AttachedSkillIds.Should().BeEquivalentTo(IntentOnly);
        loaded.SelectedSkillIdsByMode[Work].Should().BeEquivalentTo(IntentOnly);
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync с пустым списком убирает attached, не трогая выбор режима")]
    public async Task SetAttachedSkillIds_empty_unsets_runtime_only()
    {
        var (db, store) = await NewScopeAsync();
        await store.SaveAsync("i-clear", Launch("work", "claude", "opus", "high"), CancellationToken.None);
        await store.SetAttachedSkillIdsAsync("i-clear", Work, IntentOnly, CancellationToken.None);

        await store.SetAttachedSkillIdsAsync("i-clear", Work, Array.Empty<string>(), CancellationToken.None);

        var raw = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", "i-clear")).FirstAsync(CancellationToken.None);
        raw.Contains("attached_skill_ids").Should().BeFalse();
        var loaded = await store.GetAsync("i-clear", CancellationToken.None);
        loaded!.AttachedSkillIds.Should().BeEmpty();
        // Per-mode selection is the operator's «next-preflight» intent — clearing the runtime
        // indicator does NOT touch it.
        loaded.SelectedSkillIdsByMode[Work].Should().BeEquivalentTo(IntentOnly);
    }

    [Fact(DisplayName = "Tolerant read: документ без attached_skill_ids/selected_skill_ids_by_mode читается как пустой")]
    public async Task Tolerant_read_legacy_doc_returns_empty_collections()
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
        loaded!.AttachedSkillIds.Should().BeEmpty();
        loaded.SelectedSkillIdsByMode.Should().BeEmpty();
    }

    [Fact(DisplayName = "SetAttachedSkillIdsAsync с upsert=false: на несуществующий интент → no-op")]
    public async Task SetAttachedSkillIds_missing_doc_is_noop()
    {
        var (db, store) = await NewScopeAsync();

        await store.SetAttachedSkillIdsAsync("i-ghost", Work, IntentOnly, CancellationToken.None);

        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.TerminalLaunches)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "i-ghost"),
                cancellationToken: CancellationToken.None);
        count.Should().Be(0);
    }

    private static TerminalLaunchRecord Launch(string mode, string vendor, string model, string? effort) =>
        new(mode, vendor, model, effort, Array.Empty<string>(), EmptySelections);

    private static void AssertAxis(
        TerminalLaunchRecord record, string mode, string vendor, string model, string? effort)
    {
        record.Mode.Should().Be(mode);
        record.Vendor.Should().Be(vendor);
        record.Model.Should().Be(model);
        record.Effort.Should().Be(effort);
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
