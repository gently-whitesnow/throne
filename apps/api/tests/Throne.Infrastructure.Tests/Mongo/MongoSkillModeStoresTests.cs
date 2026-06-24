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
        var (db, defaults) = await NewScopeAsync();
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
        var (_, defaults) = await NewScopeAsync();
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
        var (db, defaults) = await NewScopeAsync();
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

    private async Task<(IMongoDatabase Db, MongoSkillModeDefaultStore Defaults)>
        NewScopeAsync()
    {
        var name = $"throne_skill_mode_{Guid.NewGuid():N}";
        await fixture.Client.DropDatabaseAsync(name);
        var db = fixture.Client.GetDatabase(name);
        var sessions = new MongoSessionAccessor();
        return (db, new MongoSkillModeDefaultStore(db, sessions));
    }
}
