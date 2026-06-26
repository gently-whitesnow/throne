using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

/// <summary>
/// Read-only aggregator for the context rail. Executes as a handful of GROUP BY queries
/// plus an in-memory unwind of the
/// JSON <c>tag_ids</c> column for tag counts — SQLite has no first-class array column,
/// and EF's LINQ translation can't push a JSON-array unwind into the provider.
/// </summary>
internal sealed class EfIntentContextReader(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions)
{
    private static readonly string[] ActiveStatuses =
    [
        IntentStatusNames.Draft, IntentStatusNames.Interview, IntentStatusNames.ReadyForWork,
        IntentStatusNames.Work, IntentStatusNames.AwaitingOperator,
    ];

    private static readonly string[] ArchiveStatuses =
    [
        IntentStatusNames.Done, IntentStatusNames.Reject,
    ];

    private static readonly string[] FridgeStatuses =
    [
        IntentStatusNames.Fridge,
    ];

    public Task<IntentContextCounts> GetContextCountsAsync(
        IReadOnlyList<string> runningTerminalIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(runningTerminalIds);
        return ReadAsync(async (ctx, c) =>
        {
            var byStatus = await ctx.Set<IntentRow>()
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(c);
            var statusMap = byStatus
                .GroupBy(
                    x => string.IsNullOrEmpty(x.Status) ? IntentStatusNames.Draft : x.Status,
                    StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count), StringComparer.Ordinal);

            var (activeUntagged, activeTagCounts) = await CountBucketAsync(ctx, ActiveStatuses, c);
            var (archiveUntagged, archiveTagCounts) = await CountBucketAsync(ctx, ArchiveStatuses, c);
            var (fridgeUntagged, fridgeTagCounts) = await CountBucketAsync(ctx, FridgeStatuses, c);

            var pinned = await ctx.Set<IntentPinRow>()
                .Select(p => p.IntentId)
                .Distinct()
                .CountAsync(c);

            var terminalRunning = 0;
            if (runningTerminalIds.Count > 0)
            {
                var ids = runningTerminalIds.ToList();
                terminalRunning = await ctx.Set<IntentRow>()
                    .CountAsync(r => ids.Contains(r.Id), c);
            }

            return new IntentContextCounts(
                InboxHelp: Lookup(statusMap, IntentStatusNames.AwaitingOperator),
                Fridge: Lookup(statusMap, IntentStatusNames.Fridge),
                Archive: Lookup(statusMap, IntentStatusNames.Done) + Lookup(statusMap, IntentStatusNames.Reject),
                Pinned: pinned,
                Untagged: activeUntagged,
                ArchiveUntagged: archiveUntagged,
                FridgeUntagged: fridgeUntagged,
                Tags: activeTagCounts,
                ArchiveTags: archiveTagCounts,
                FridgeTags: fridgeTagCounts,
                TerminalRunning: terminalRunning);
        }, ct);
    }

    private static async Task<(int Untagged, List<IntentTagCount> TagCounts)> CountBucketAsync(
        ThroneDbContext ctx,
        string[] statuses,
        CancellationToken ct)
    {
        // Materialize just the tag ids for rows in the bucket — text payload stays in
        // SQLite and the configured converter handles the JSON column.
        var includeDraft = statuses.Contains(IntentStatusNames.Draft, StringComparer.Ordinal);
        var rows = await ctx.Set<IntentRow>()
            .Where(r => statuses.Contains(r.Status) || (includeDraft && r.Status == string.Empty))
            .Select(r => r.TagIds)
            .ToListAsync(ct);

        var untagged = 0;
        var tagCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var tagIds in rows)
        {
            if (tagIds.Count == 0)
            {
                untagged++;
                continue;
            }

            foreach (var id in tagIds.Where(id => !string.IsNullOrEmpty(id)))
            {
                tagCounts[id] = tagCounts.TryGetValue(id, out var n) ? n + 1 : 1;
            }
        }

        var list = tagCounts.Select(kv => new IntentTagCount(kv.Key, kv.Value)).ToList();
        return (untagged, list);
    }

    private static int Lookup(Dictionary<string, int> map, string status) =>
        map.TryGetValue(status, out var value) ? value : 0;
}
