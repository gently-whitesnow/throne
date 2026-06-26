using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.Mongo;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class MongoSkillModeStoresTests(SqliteFixture fixture)
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
        await using var ctx = await db.CreateContextAsync();
        var count = await ctx.Set<SkillModeDefaultRow>().AsNoTracking().CountAsync(CancellationToken.None);
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

    private async Task<(SqliteTestDatabase Db, ISkillModeDefaultStore Defaults)>
        NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<ISkillModeDefaultStore>());
    }
}
