using Microsoft.EntityFrameworkCore;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Links;

/// <summary>
/// Single-row lookups for the link repository — endpoint existence + (from, to) edge probe.
/// </summary>
internal static class EfIntentLinkQueries
{
    public static async Task<string?> FindMissingEndpointAsync(
        ThroneDbContext ctx,
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var from = fromId.Value;
        var to = toId.Value;
        var found = await ctx.Set<IntentRow>()
            .Where(r => r.Id == from || r.Id == to)
            .Select(r => r.Id)
            .ToListAsync(ct);
        var set = found.ToHashSet(StringComparer.Ordinal);
        if (!set.Contains(from))
        {
            return from;
        }
        if (!set.Contains(to))
        {
            return to;
        }
        return null;
    }

    public static Task<IntentLinkRow?> FindEdgeAsync(
        ThroneDbContext ctx,
        IntentId fromId,
        IntentId toId,
        CancellationToken ct)
    {
        var from = fromId.Value;
        var to = toId.Value;
        return ctx.Set<IntentLinkRow>()
            .FirstOrDefaultAsync(r => r.FromId == from && r.ToId == to, ct);
    }
}
