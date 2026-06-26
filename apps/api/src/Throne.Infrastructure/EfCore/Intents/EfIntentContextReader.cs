using Microsoft.EntityFrameworkCore;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

/// <summary>
/// Read-only aggregator for the context rail. Mirrors <c>MongoIntentContextReader</c>'s
/// surface but executes as a handful of GROUP BY queries plus an in-memory unwind of the
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
            var statusMap = byStatus.ToDictionary(
                x => string.IsNullOrEmpty(x.Status) ? IntentStatusNames.Draft : x.Status,
                x => x.Count,
                StringComparer.Ordinal);

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
        // Materialize just the JSON tag-ids strings for rows in the bucket — text payload
        // stays in SQLite. We rely on the fact that tag_ids is a JSON-array column whose
        // empty form is the literal "[]".
        var rows = await ctx.Set<IntentRow>()
            .Where(r => statuses.Contains(r.Status))
            .Select(r => EF.Property<string>(r, "tag_ids"))
            .ToListAsync(ct);

        var untagged = 0;
        var tagCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var json in rows)
        {
            if (string.IsNullOrEmpty(json) || string.Equals(json, "[]", StringComparison.Ordinal))
            {
                untagged++;
                continue;
            }

            // tag_ids is a JSON array of strings. We avoid System.Text.Json here — the
            // payload is a strict array shape produced by IntentRowConfiguration, so a tiny
            // tokenizer keeps the hot path allocation-light. Each id is a GUID hex (no
            // embedded quotes), so a quoted-substring scan is sound.
            var hasAny = false;
            var idx = 0;
            while (true)
            {
                var openQuote = json.IndexOf('"', idx);
                if (openQuote < 0)
                {
                    break;
                }
                var closeQuote = json.IndexOf('"', openQuote + 1);
                if (closeQuote < 0)
                {
                    break;
                }
                var id = json[(openQuote + 1)..closeQuote];
                if (!string.IsNullOrEmpty(id))
                {
                    tagCounts[id] = tagCounts.TryGetValue(id, out var n) ? n + 1 : 1;
                    hasAny = true;
                }
                idx = closeQuote + 1;
            }
            if (!hasAny)
            {
                untagged++;
            }
        }

        var list = tagCounts.Select(kv => new IntentTagCount(kv.Key, kv.Value)).ToList();
        return (untagged, list);
    }

    private static int Lookup(Dictionary<string, int> map, string status) =>
        map.TryGetValue(status, out var value) ? value : 0;
}
