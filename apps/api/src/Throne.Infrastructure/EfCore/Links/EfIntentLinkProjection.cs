using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Linking;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Links;

/// <summary>
/// Shared link → <see cref="IntentLinkView"/> projection helpers. Lives next to the link
/// repository so the repo type itself stays well under the per-type LOC budget.
/// </summary>
internal static class EfIntentLinkProjection
{
    public static async Task<List<IntentLinkView>> ProjectAsync(
        ThroneDbContext ctx,
        IntentId intentId,
        List<IntentLinkRow> rows,
        CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return [];
        }
        var peerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            peerIds.Add(string.Equals(r.FromId, intentId.Value, StringComparison.Ordinal) ? r.ToId : r.FromId);
        }
        var peersById = await LoadPeersAsync(ctx, peerIds, ct);
        var result = new List<IntentLinkView>(rows.Count);
        foreach (var r in rows)
        {
            var direction = string.Equals(r.FromId, intentId.Value, StringComparison.Ordinal)
                ? IntentLinkDirection.Outgoing
                : IntentLinkDirection.Incoming;
            var peerId = direction == IntentLinkDirection.Outgoing ? r.ToId : r.FromId;
            if (peersById.TryGetValue(peerId, out var peer))
            {
                result.Add(new IntentLinkView(
                    IntentLinkRowMapper.ToDomain(r),
                    direction,
                    IntentRowMapper.ToDomain(peer)));
            }
        }
        return result;
    }

    public static async Task<Dictionary<string, IntentRow>> LoadPeersAsync(
        ThroneDbContext ctx,
        HashSet<string> peerIds,
        CancellationToken ct)
    {
        if (peerIds.Count == 0)
        {
            return new Dictionary<string, IntentRow>(StringComparer.Ordinal);
        }
        var rows = await ctx.Set<IntentRow>()
            .Where(r => peerIds.Contains(r.Id))
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r, StringComparer.Ordinal);
    }

    public static void AppendView(
        Dictionary<string, List<IntentLinkView>> bucket,
        string ownerId,
        IntentLink link,
        IntentLinkDirection direction,
        Intent peer)
    {
        if (!bucket.TryGetValue(ownerId, out var list))
        {
            list = [];
            bucket[ownerId] = list;
        }
        list.Add(new IntentLinkView(link, direction, peer));
    }
}
