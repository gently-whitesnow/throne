using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Terminals;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoSkillModeStoresTests(MongoFixture fixture)
{
    [Fact(DisplayName = "SkillModeDefault: UpsertMissingAsync сидит только отсутствующие значения")]
    public async Task Default_store_upserts_only_missing_values()
    {
        var (db, defaults, _) = await NewScopeAsync();
        await defaults.ReplaceAsync(
            [new SkillModeDefault(TerminalRunModes.Work, SessionSkillPackageIds.Intent, true)],
            CancellationToken.None);

        await defaults.UpsertMissingAsync(
            [
                new SkillModeDefault(TerminalRunModes.Work, SessionSkillPackageIds.Intent, false),
                new SkillModeDefault(TerminalRunModes.Review, SessionSkillPackageIds.Review, true),
            ],
            CancellationToken.None);

        (await defaults.ListAsync(CancellationToken.None)).Should().BeEquivalentTo(
            [
                new SkillModeDefault(TerminalRunModes.Work, SessionSkillPackageIds.Intent, true),
                new SkillModeDefault(TerminalRunModes.Review, SessionSkillPackageIds.Review, true),
            ]);
        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.SkillModeDefaults)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: CancellationToken.None);
        count.Should().Be(2);
    }

    [Fact(DisplayName = "SkillModeDefault: ReplaceAsync перезаписывает значение ключа mode+skill_id")]
    public async Task Default_store_replaces_existing_values()
    {
        var (_, defaults, _) = await NewScopeAsync();
        var key = new SkillModeDefault(TerminalRunModes.Interview, SessionSkillPackageIds.Intent, true);
        await defaults.ReplaceAsync([key], CancellationToken.None);

        await defaults.ReplaceAsync([key with { Enabled = false }], CancellationToken.None);

        (await defaults.ListAsync(CancellationToken.None)).Should()
            .ContainSingle()
            .Which.Should().Be(key with { Enabled = false });
    }

    [Fact(DisplayName = "SkillModeDefault tolerant read: лишние persisted-поля игнорируются")]
    public async Task Default_store_tolerant_read_ignores_unknown_fields()
    {
        var (db, defaults, _) = await NewScopeAsync();
        await db.GetCollection<BsonDocument>(MongoCollectionNames.SkillModeDefaults).InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = "work:legacy-skill",
                ["mode"] = TerminalRunModes.Work,
                ["skill_id"] = "legacy-skill",
                ["enabled"] = true,
                ["legacy"] = "ignored",
            },
            cancellationToken: CancellationToken.None);

        (await defaults.ListAsync(CancellationToken.None)).Should().ContainSingle()
            .Which.Should().Be(new SkillModeDefault(TerminalRunModes.Work, "legacy-skill", true));
    }

    [Fact(DisplayName = "SkillModeSelection: SaveAsync хранит выбор по mode в одном документе интента")]
    public async Task Selection_store_keeps_per_mode_choices_in_one_intent_document()
    {
        var (db, _, selections) = await NewScopeAsync();

        await selections.SaveAsync(
            "intent-1",
            TerminalRunModes.Work,
            [SessionSkillPackageIds.Intent, SessionSkillPackageIds.Intent],
            CancellationToken.None);
        await selections.SaveAsync(
            "intent-1",
            TerminalRunModes.Review,
            [SessionSkillPackageIds.Review],
            CancellationToken.None);

        (await selections.GetAsync("intent-1", TerminalRunModes.Work, CancellationToken.None))
            .Should().Equal(SessionSkillPackageIds.Intent);
        (await selections.GetAsync("intent-1", TerminalRunModes.Review, CancellationToken.None))
            .Should().Equal(SessionSkillPackageIds.Review);
        var count = await db.GetCollection<BsonDocument>(MongoCollectionNames.SkillModeSelections)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("_id", "intent-1"), cancellationToken: CancellationToken.None);
        count.Should().Be(1);
    }

    [Fact(DisplayName = "SkillModeSelection tolerant read: лишние persisted-поля игнорируются")]
    public async Task Selection_store_tolerant_read_ignores_unknown_fields()
    {
        var (db, _, selections) = await NewScopeAsync();
        await db.GetCollection<BsonDocument>(MongoCollectionNames.SkillModeSelections).InsertOneAsync(
            new BsonDocument
            {
                ["_id"] = "intent-legacy",
                ["mode_selections"] = new BsonArray
                {
                    new BsonDocument
                    {
                        ["mode"] = TerminalRunModes.Work,
                        ["selected_skill_ids"] = new BsonArray { SessionSkillPackageIds.Intent },
                        ["legacy"] = "ignored",
                    },
                },
                ["legacy_root"] = true,
            },
            cancellationToken: CancellationToken.None);

        (await selections.GetAsync("intent-legacy", TerminalRunModes.Work, CancellationToken.None))
            .Should().Equal(SessionSkillPackageIds.Intent);
    }

    [Fact(DisplayName = "SkillModeSelection: GetAsync для отсутствующего mode возвращает null")]
    public async Task Selection_store_missing_mode_returns_null()
    {
        var (_, _, selections) = await NewScopeAsync();

        await selections.SaveAsync(
            "intent-2",
            TerminalRunModes.Work,
            [SessionSkillPackageIds.Intent],
            CancellationToken.None);

        (await selections.GetAsync("intent-2", TerminalRunModes.Free, CancellationToken.None))
            .Should().BeNull();
    }

    private async Task<(IMongoDatabase Db, MongoSkillModeDefaultStore Defaults, MongoIntentSkillModeSelectionStore Selections)>
        NewScopeAsync()
    {
        var name = $"throne_skill_mode_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        return (
            db,
            new MongoSkillModeDefaultStore(db, sessions),
            new MongoIntentSkillModeSelectionStore(db, sessions));
    }
}
