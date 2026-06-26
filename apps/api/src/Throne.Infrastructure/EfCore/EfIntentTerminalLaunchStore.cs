using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="IIntentTerminalLaunchStore"/> (ADR-0041). One row per intent;
/// upserted on every successful spawn. The save axis (mode/vendor/model/effort) is
/// auxiliary UI-prefill state, so the store does not require an ambient unit of work and
/// opens a one-shot context when called outside one — matching the persistence store's behavior.
/// </summary>
internal sealed class EfIntentTerminalLaunchStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentTerminalLaunchStore
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptySelections =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    private readonly IDbContextFactory<ThroneDbContext> _contextFactory = contextFactory;

    public Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        return ReadAsync(async (ctx, c) =>
        {
            var row = await ctx.Set<IntentTerminalLaunchRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == intentId, c);
            return row is null ? null : MapToRecord(row);
        }, ct);
    }

    public Task SaveAsync(string intentId, TerminalLaunchRecord record, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(record);

        return ExecuteAsync(async (ctx, c) =>
        {
            var row = await GetOrCreateAsync(ctx, intentId, c);
            row.Mode = record.Mode;
            row.Vendor = record.Vendor;
            row.Model = record.Model;
            // Effort-less vendors (opencode) carry no effort: drop the field so a later
            // switch to an effort vendor never reads a stale value.
            row.Effort = record.Effort;
            // selected_skill_ids_by_mode is intentionally NOT touched: the per-mode selection
            // survives a respawn until explicitly updated via SaveSelectedSkillIdsAsync.
            await ctx.SaveChangesAsync(c);
        }, ct);
    }

    public Task SaveSelectedSkillIdsAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> selectedSkillIds,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);
        ArgumentNullException.ThrowIfNull(selectedSkillIds);

        return ExecuteAsync(async (ctx, c) =>
        {
            var row = await GetOrCreateAsync(ctx, intentId, c);
            row.SelectedSkillIdsByMode ??= new Dictionary<string, List<string>>(StringComparer.Ordinal);
            row.SelectedSkillIdsByMode[mode] = selectedSkillIds
                .Distinct(StringComparer.Ordinal).ToList();
            await ctx.SaveChangesAsync(c);
        }, ct);
    }

    private static async Task<IntentTerminalLaunchRow> GetOrCreateAsync(
        ThroneDbContext ctx, string intentId, CancellationToken ct)
    {
        var row = await ctx.Set<IntentTerminalLaunchRow>()
            .FirstOrDefaultAsync(r => r.Id == intentId, ct);
        if (row is not null)
        {
            return row;
        }
        row = new IntentTerminalLaunchRow { Id = intentId };
        ctx.Set<IntentTerminalLaunchRow>().Add(row);
        return row;
    }

    private async Task ExecuteAsync(
        Func<ThroneDbContext, CancellationToken, Task> work, CancellationToken ct)
    {
        var ambient = Sessions.Current;
        if (ambient is not null)
        {
            await work(ambient, ct);
            return;
        }
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await work(context, ct);
    }

    private static TerminalLaunchRecord MapToRecord(IntentTerminalLaunchRow row)
    {
        var selections = row.SelectedSkillIdsByMode is { Count: > 0 }
            ? row.SelectedSkillIdsByMode.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyList<string>)kv.Value.ToArray(),
                StringComparer.Ordinal)
            : EmptySelections;
        return new TerminalLaunchRecord(row.Mode, row.Vendor, row.Model, row.Effort, selections);
    }
}
