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
/// A board's attached cards surface as their own facet group (counted separately); attaching a card
/// is read-only context and does NOT move the intent out of its tag/untagged bucket (ADR-0052).
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

            var (activeUntagged, activeTagCounts) = await CountActiveBucketAsync(ctx, c);
            var boardCounts = await CountBoardsAsync(ctx, c);
            var (archiveUntagged, archiveTagCounts) = await CountBucketAsync(ctx, ArchiveStatuses, c);
            var (fridgeUntagged, fridgeTagCounts) = await CountBucketAsync(ctx, FridgeStatuses, c);

            var boards = await BuildBoardsAsync(ctx, boardCounts, c);

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
                Boards: boards,
                TerminalRunning: terminalRunning);
        }, ct);
    }

    // Active bucket counts tags/untagged over all active intents. An attached card is read-only context
    // (ADR-0052) and never diverts the intent out of this bucket — board grouping is a separate facet
    // computed by CountBoardsAsync.
    private static async Task<(int Untagged, List<IntentTagCount> TagCounts)> CountActiveBucketAsync(
        ThroneDbContext ctx,
        CancellationToken ct)
    {
        var rows = await ctx.Set<IntentRow>()
            .Where(r => ActiveStatuses.Contains(r.Status) || r.Status == string.Empty)
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

    // Board facet: for each (tracker, board) that has an attachment on an active intent, count the
    // DISTINCT active intents attached to it. Cards are 1:N (an intent can attach several cards on the
    // same board — deduped here) and availability is NOT filtered (a Gone card is still attached).
    private static async Task<Dictionary<(string Tracker, string BoardId), int>> CountBoardsAsync(
        ThroneDbContext ctx,
        CancellationToken ct)
    {
        var rows = await ctx.Set<IntentCardAttachmentRow>()
            .Join(
                ctx.Set<IntentRow>().Where(i => ActiveStatuses.Contains(i.Status) || i.Status == string.Empty),
                a => a.IntentId,
                i => i.Id,
                (a, i) => new { a.Tracker, a.BoardId, a.IntentId })
            .ToListAsync(ct);

        var byBoard = new Dictionary<(string Tracker, string BoardId), HashSet<string>>();
        foreach (var row in rows)
        {
            var key = (row.Tracker, row.BoardId);
            if (!byBoard.TryGetValue(key, out var intents))
            {
                intents = new HashSet<string>(StringComparer.Ordinal);
                byBoard[key] = intents;
            }

            intents.Add(row.IntentId);
        }

        return byBoard.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
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

    // Board groups come from the saved connections (so empty boards still show as a group), unioned
    // with any board that still has an attached card but was deselected — those attachments must not vanish.
    private static async Task<List<IntentBoardCount>> BuildBoardsAsync(
        ThroneDbContext ctx,
        Dictionary<(string Tracker, string BoardId), int> boardCounts,
        CancellationToken ct)
    {
        var connections = await ctx.Set<TaskTrackerConnectionRow>().ToListAsync(ct);
        var ordered = new List<(string Tracker, string BoardId, string? Title)>();
        var seen = new HashSet<(string, string)>();
        foreach (var conn in connections)
        {
            foreach (var board in conn.SelectedBoards)
            {
                var key = (conn.Tracker, board.BoardId);
                if (seen.Add(key))
                {
                    ordered.Add((conn.Tracker, board.BoardId, board.BoardTitle));
                }
            }
        }

        foreach (var key in boardCounts.Keys)
        {
            if (seen.Add(key))
            {
                ordered.Add((key.Tracker, key.BoardId, null));
            }
        }

        return ordered
            .Select(o => new IntentBoardCount(
                o.Tracker,
                o.BoardId,
                o.Title,
                boardCounts.TryGetValue((o.Tracker, o.BoardId), out var n) ? n : 0))
            .ToList();
    }

    private static int Lookup(Dictionary<string, int> map, string status) =>
        map.TryGetValue(status, out var value) ? value : 0;
}
