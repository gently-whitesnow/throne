using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Driver;
using Throne.Application.Instructions.Manifest;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoPromptPartSeederTests(MongoFixture fixture)
{
    private const string LegacyCollection = "instructions";

    [Fact(DisplayName = "Seeder засевает system-части из манифеста и мигрирует user-инструкции как первую версию, дропая legacy")]
    public async Task Seeds_system_migrates_user_and_retires_legacy()
    {
        var db = NewDatabase();
        await SeedLegacyAsync(db);

        var (seeder, parts, versions) = Build(db, Manifest(("common", "sys common"), ("work", "sys work")));
        await seeder.RunAsync(CancellationToken.None);

        var sysCommon = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "common", CancellationToken.None);
        sysCommon.Should().NotBeNull();
        sysCommon!.Id.Value.Should().Be("system:common", "MCP bundle text keeps the legacy synthetic system id");
        sysCommon!.Text.Should().Be("sys common", "system text comes from the manifest, not the legacy instructions doc");
        sysCommon.CurrentVersion.Should().Be(1);
        sysCommon.ModeRoles.Should().ContainSingle(r =>
            r.Mode == "work" && r.Role == PromptPartRoleNames.Mandatory && r.Order == 0);

        var userWork = await parts.GetByScopeKeyAsync(PromptPartScopeNames.User, "work", CancellationToken.None);
        userWork.Should().NotBeNull();
        userWork!.Id.Value.Should().Be("i-work", "migration preserves legacy instruction id in the PromptPart id");
        userWork!.Text.Should().Be("legacy user work");
        userWork.CurrentVersion.Should().Be(1, "migration imports the current text as the first version without backfilling history");
        userWork.ModeRoles.Should().ContainSingle(r =>
            r.Mode == "work" && r.Role == PromptPartRoleNames.Mandatory && r.Order == 3);

        var history = await versions.ListByOwnerAsync(TextVersionOwnerKind.PromptPart, userWork.Id.Value, CancellationToken.None);
        history.Should().HaveCount(1);

        (await CollectionExistsAsync(db, LegacyCollection)).Should().BeFalse("legacy instructions collection is retired on start");
        var legacyVersions = await db.GetCollection<BsonDocument>(MongoCollectionNames.TextVersions)
            .Find(new BsonDocument("owner_kind", "instruction")).ToListAsync();
        legacyVersions.Should().BeEmpty("legacy instruction text_versions are purged");
    }

    [Fact(DisplayName = "Seeder идемпотентен и при дрейфе текста манифеста пишет новую версию system-части")]
    public async Task Idempotent_and_reconciles_text_drift()
    {
        var db = NewDatabase();

        var (seeder1, parts, versions) = Build(db, Manifest(("common", "sys common")));
        await seeder1.RunAsync(CancellationToken.None);
        await seeder1.RunAsync(CancellationToken.None); // second pass: no duplicates, no throw

        var afterIdempotent = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "common", CancellationToken.None);
        afterIdempotent!.CurrentVersion.Should().Be(1);

        var (seeder2, _, _) = Build(db, Manifest(("common", "sys common v2")));
        await seeder2.RunAsync(CancellationToken.None);

        var drifted = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "common", CancellationToken.None);
        drifted!.Text.Should().Be("sys common v2");
        drifted.CurrentVersion.Should().Be(2);
        var history = await versions.ListByOwnerAsync(TextVersionOwnerKind.PromptPart, drifted.Id.Value, CancellationToken.None);
        history.Should().HaveCount(2, "drift appends a new version on top of the seeded snapshot");
    }

    private static SkillManifest Manifest(params (string Kind, string Text)[] system) =>
        new(
            Version: 1,
            SystemInstructions: system.Select(s => new SystemInstructionEntry(s.Kind, s.Text)).ToList(),
            Bundles:
            [
                new BundleDefinition("work",
                [
                    new BundleInclude("system", "common"),
                    new BundleInclude("system", "work"),
                    new BundleInclude("user", "common"),
                    new BundleInclude("user", "work"),
                ]),
            ],
            DreamSources: []);

    private static async Task SeedLegacyAsync(IMongoDatabase db)
    {
        await db.GetCollection<BsonDocument>(LegacyCollection).InsertManyAsync(
        [
            new BsonDocument { { "_id", "i-work" }, { "scope", "user" }, { "kind", "work" }, { "text", "legacy user work" }, { "current_version", 4 } },
            new BsonDocument { { "_id", "i-sys" }, { "scope", "system" }, { "kind", "common" }, { "text", "ignore me" }, { "current_version", 1 } },
        ]);
        await db.GetCollection<BsonDocument>(MongoCollectionNames.TextVersions).InsertOneAsync(
            new BsonDocument { { "_id", "tv-legacy" }, { "owner_kind", "instruction" }, { "owner_id", "i-work" }, { "version", 1 } });
    }

    private (PromptPartSeeder Seeder, MongoPromptPartRepository Parts, MongoTextVersionRepository Versions) Build(
        IMongoDatabase db, SkillManifest manifest)
    {
        var session = new MongoSessionAccessor();
        var uow = new MongoUnitOfWork(fixture.Client, session);
        var parts = new MongoPromptPartRepository(db, session);
        var versions = new MongoTextVersionRepository(db, session);
        var seeder = new PromptPartSeeder(db, new InMemorySkillManifestProvider(manifest), parts, uow, TimeProvider.System);
        return (seeder, parts, versions);
    }

    private IMongoDatabase NewDatabase() => fixture.Client.GetDatabase($"throne_test_{Guid.NewGuid():N}");

    private static async Task<bool> CollectionExistsAsync(IMongoDatabase db, string name)
    {
        using var cursor = await db.ListCollectionNamesAsync(
            new ListCollectionNamesOptions { Filter = new BsonDocument("name", name) });
        return await cursor.AnyAsync();
    }
}
