using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.Tests.EfCore.Persistence;

[Collection(nameof(SqliteIntegrationFixture))]
[Trait("Category", "Integration")]
public class EfCoreIntentTerminalLaunchStoreTests(SqliteFixture fixture)
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
        loaded!.SelectedSkillIdsByMode.Should().BeEmpty();
    }

    [Fact(DisplayName = "Повторный SaveAsync перезаписывает последний выбор (один документ на интент)")]
    public async Task Save_is_idempotent_upsert()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-2", Launch("interview", "claude", "opus", "high"), CancellationToken.None);
        await store.SaveAsync("i-2", Launch("review", "codex", "gpt-5.6-terra", "low"), CancellationToken.None);

        var loaded = await store.GetAsync("i-2", CancellationToken.None);
        AssertAxis(loaded!, "review", "codex", "gpt-5.6-terra", "low");
        var count = await CountRowsAsync(db, "i-2");
        count.Should().Be(1);
    }

    [Fact(DisplayName = "Effort=null у вендора без оси усилия не пишется и читается как null")]
    public async Task Null_effort_round_trips()
    {
        var (db, store) = await NewScopeAsync();

        await store.SaveAsync("i-3", Launch("work", "opencode", "throne-local/x", null), CancellationToken.None);

        var loaded = await store.GetAsync("i-3", CancellationToken.None);
        AssertAxis(loaded!, "work", "opencode", "throne-local/x", null);
        var raw = await FindRowAsync(db, "i-3");
        raw!.Effort.Should().BeNull();
    }

    [Fact(DisplayName = "Direct row read восстанавливает launch axis")]
    public async Task Direct_row_read_restores_axis()
    {
        var (db, store) = await NewScopeAsync();
        await using (var ctx = await db.CreateContextAsync())
        {
            ctx.Set<IntentTerminalLaunchRow>().Add(new IntentTerminalLaunchRow
            {
                Id = "i-legacy",
                Mode = "work",
                Vendor = "claude",
                Model = "sonnet",
                Effort = "medium",
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        var loaded = await store.GetAsync("i-legacy", CancellationToken.None);
        AssertAxis(loaded!, "work", "claude", "sonnet", "medium");
    }

    [Fact(DisplayName = "GetAsync на не запускавшийся интент → null")]
    public async Task Get_missing_returns_null()
    {
        var (_, store) = await NewScopeAsync();

        (await store.GetAsync("i-missing", CancellationToken.None)).Should().BeNull();
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

    [Fact(DisplayName = "SaveAsync (рестарт оси) не сбрасывает выбор по режимам")]
    public async Task Save_keeps_per_mode_selection()
    {
        var (_, store) = await NewScopeAsync();
        await store.SaveAsync("i-keep", Launch("work", "claude", "opus", "high"), CancellationToken.None);
        await store.SaveSelectedSkillIdsAsync("i-keep", Work, IntentOnly, CancellationToken.None);

        // Restart-style overwrite: new launch axis, no selection change requested.
        await store.SaveAsync("i-keep", Launch("review", "claude", "sonnet", "medium"), CancellationToken.None);

        var loaded = await store.GetAsync("i-keep", CancellationToken.None);
        AssertAxis(loaded!, "review", "claude", "sonnet", "medium");
        loaded!.SelectedSkillIdsByMode[Work].Should().BeEquivalentTo(IntentOnly);
    }

    [Fact(DisplayName = "Row без selected_skill_ids_by_mode читается как пустой")]
    public async Task Row_without_skill_columns_returns_empty_collections()
    {
        var (db, store) = await NewScopeAsync();
        await using (var ctx = await db.CreateContextAsync())
        {
            ctx.Set<IntentTerminalLaunchRow>().Add(new IntentTerminalLaunchRow
            {
                Id = "i-legacy-attach",
                Mode = "work",
                Vendor = "claude",
                Model = "sonnet",
                Effort = "medium",
            });
            await ctx.SaveChangesAsync(CancellationToken.None);
        }

        var loaded = await store.GetAsync("i-legacy-attach", CancellationToken.None);
        loaded!.SelectedSkillIdsByMode.Should().BeEmpty();
    }

    private static TerminalLaunchRecord Launch(string mode, string vendor, string model, string? effort) =>
        new(mode, vendor, model, effort, EmptySelections);

    private static void AssertAxis(
        TerminalLaunchRecord record, string mode, string vendor, string model, string? effort)
    {
        record.Mode.Should().Be(mode);
        record.Vendor.Should().Be(vendor);
        record.Model.Should().Be(model);
        record.Effort.Should().Be(effort);
    }

    private async Task<(SqliteTestDatabase Db, IIntentTerminalLaunchStore Store)> NewScopeAsync()
    {
        var db = await fixture.CreateDatabaseAsync();
        return (db, db.GetRequiredService<IIntentTerminalLaunchStore>());
    }

    private static async Task<IntentTerminalLaunchRow?> FindRowAsync(SqliteTestDatabase db, string id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentTerminalLaunchRow>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    }

    private static async Task<int> CountRowsAsync(SqliteTestDatabase db, string id)
    {
        await using var ctx = await db.CreateContextAsync();
        return await ctx.Set<IntentTerminalLaunchRow>().AsNoTracking().CountAsync(x => x.Id == id);
    }
}
