using Microsoft.EntityFrameworkCore;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Pins;

/// <summary>
/// Single-row helpers + pivot/tail lookup for the pin repository. Hosted as static
/// utilities so the repo type itself stays well under the per-type LOC budget.
/// </summary>
internal static class EfIntentPinQueries
{
    public static Task<IntentRow?> LoadIntentAsync(ThroneDbContext ctx, IntentId intentId, CancellationToken ct)
    {
        var id = intentId.Value;
        return ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public static Task<bool> TagExistsAsync(ThroneDbContext ctx, string tagId, CancellationToken ct)
    {
        // Hand-rolled probe because the Tag aggregate's row type is owned by a later slice;
        // the column shape (id) is a stable schema contract. SqlQuery (interpolated) makes
        // `tagId` a SQL parameter while the table name stays a compile-time literal.
        return ctx.Database
            .SqlQuery<int>($"SELECT 1 AS Value FROM tags WHERE id = {tagId}")
            .AnyAsync(ct);
    }

    public static Task<IntentPinRow?> FindExistingPinAsync(
        ThroneDbContext ctx,
        IntentId intentId,
        TagId contextTagId,
        CancellationToken ct)
    {
        var i = intentId.Value;
        var c = contextTagId.Value;
        return ctx.Set<IntentPinRow>()
            .FirstOrDefaultAsync(r => r.IntentId == i && r.ContextTagId == c, ct);
    }

    public static async Task<(string? BeforeKey, string? AfterKey, string? Missing)> ResolvePivotKeysAsync(
        ThroneDbContext ctx,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            return (null, null, null);
        }

        var ids = new List<string>(2);
        if (beforeId is not null)
        {
            ids.Add(beforeId.Value.Value);
        }
        if (afterId is not null)
        {
            ids.Add(afterId.Value.Value);
        }
        var contextWire = contextTagId.Value;
        var rows = await ctx.Set<IntentPinRow>()
            .Where(r => r.ContextTagId == contextWire && ids.Contains(r.IntentId))
            .Select(r => new { r.IntentId, r.PinSortKey })
            .ToListAsync(ct);

        string? Lookup(IntentId? id) => id is null
            ? null
            : rows.FirstOrDefault(d => d.IntentId == id.Value.Value)?.PinSortKey;

        var beforeKey = Lookup(beforeId);
        var afterKey = Lookup(afterId);
        if (beforeId is not null && beforeKey is null)
        {
            return (null, null, beforeId.Value.Value);
        }
        if (afterId is not null && afterKey is null)
        {
            return (null, null, afterId.Value.Value);
        }
        return (beforeKey, afterKey, null);
    }

    public static async Task<string?> GetTailKeyAsync(
        ThroneDbContext ctx,
        TagId contextTagId,
        CancellationToken ct)
    {
        var contextWire = contextTagId.Value;
        var key = await ctx.Set<IntentPinRow>()
            .Where(r => r.ContextTagId == contextWire)
            .OrderByDescending(r => r.PinSortKey)
            .Select(r => r.PinSortKey)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrEmpty(key) ? null : key;
    }
}
