using FluentAssertions;
using MongoDB.Driver;
using Throne.Application.Manifest;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(MongoIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoPromptPartSeederTests(MongoFixture fixture)
{
    [Fact(DisplayName = "Seeder засевает system-части из манифеста как первую версию с mandatory-ролями")]
    public async Task Seeds_system_parts_from_manifest()
    {
        var db = NewDatabase();

        var (seeder, parts, _) = Build(db, Manifest(("common", "sys common"), ("work", "sys work")));
        await seeder.RunAsync(CancellationToken.None);

        var sysCommon = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "common", CancellationToken.None);
        sysCommon.Should().NotBeNull();
        sysCommon!.Id.Value.Should().Be("system:common", "MCP bundle text keeps the synthetic system id");
        sysCommon!.Text.Should().Be("sys common", "system text comes from the manifest");
        sysCommon.CurrentVersion.Should().Be(1);
        sysCommon.ModeRoles.Should().ContainSingle(r =>
            r.Mode == "work" && r.Role == PromptPartRoleNames.Mandatory && r.Order == 0);

        var sysWork = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "work", CancellationToken.None);
        sysWork.Should().NotBeNull();
        sysWork!.Text.Should().Be("sys work");
        sysWork.CurrentVersion.Should().Be(1);
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

    [Fact(DisplayName = "Seeder вычищает system-часть, которую манифест больше не объявляет, вместе с её mode-ролями")]
    public async Task Purges_orphaned_system_part_dropped_from_manifest()
    {
        var db = NewDatabase();

        var (seeder1, parts, _) = Build(db, Manifest(("common", "sys common"), ("work", "sys work")));
        await seeder1.RunAsync(CancellationToken.None);

        var before = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "common", CancellationToken.None);
        before.Should().NotBeNull();
        before!.ModeRoles.Should().NotBeEmpty("seeded common carries a mandatory work-role from the bundle");

        var (seeder2, _, _) = Build(db, ManifestWithoutCommon(("work", "sys work")));
        await seeder2.RunAsync(CancellationToken.None);
        await seeder2.RunAsync(CancellationToken.None); // idempotent: orphan already gone, nothing to purge

        var orphan = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "common", CancellationToken.None);
        orphan.Should().BeNull("manifest no longer declares common, so the orphan is removed");

        var work = await parts.GetByScopeKeyAsync(PromptPartScopeNames.System, "work", CancellationToken.None);
        work.Should().NotBeNull("system parts still declared in the manifest survive the purge");
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

    private static SkillManifest ManifestWithoutCommon(params (string Kind, string Text)[] system) =>
        new(
            Version: 1,
            SystemInstructions: system.Select(s => new SystemInstructionEntry(s.Kind, s.Text)).ToList(),
            Bundles:
            [
                new BundleDefinition("work",
                [
                    new BundleInclude("system", "work"),
                    new BundleInclude("user", "common"),
                    new BundleInclude("user", "work"),
                ]),
            ],
            DreamSources: []);

    private (PromptPartSeeder Seeder, MongoPromptPartRepository Parts, MongoTextVersionRepository Versions) Build(
        IMongoDatabase db, SkillManifest manifest)
    {
        var session = new MongoSessionAccessor();
        var uow = new MongoUnitOfWork(fixture.Client, session);
        var parts = new MongoPromptPartRepository(db, session);
        var versions = new MongoTextVersionRepository(db, session);
        var seeder = new PromptPartSeeder(new InMemorySkillManifestProvider(manifest), parts, uow, TimeProvider.System);
        return (seeder, parts, versions);
    }

    private IMongoDatabase NewDatabase() => fixture.Client.GetDatabase($"throne_test_{Guid.NewGuid():N}");
}
