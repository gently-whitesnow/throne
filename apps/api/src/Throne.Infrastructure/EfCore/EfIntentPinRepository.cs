using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

internal sealed class EfIntentPinRepository(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentPinRepository
{
    public async Task<PinIntentOutcome> PinAsync(
        IntentId intentId,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(PinAsync));

        var intent = await LoadIntentAsync(ctx, intentId, ct);
        if (intent is null)
        {
            return new PinIntentOutcome.IntentNotFound();
        }

        var contextWire = contextTagId.Value;
        var tagExists = await TagExistsAsync(ctx, contextWire, ct);
        if (!tagExists)
        {
            return new PinIntentOutcome.ContextTagNotFound(contextWire);
        }

        var (beforeKey, afterKey, pivotMissing) = await ResolvePivotKeysAsync(ctx, contextTagId, beforeId, afterId, ct);
        if (pivotMissing is not null)
        {
            return new PinIntentOutcome.PivotNotFound(pivotMissing);
        }

        var existing = await FindExistingPinAsync(ctx, intentId, contextTagId, ct);
        var reorderRequested = beforeId is not null || afterId is not null;

        if (existing is not null && !reorderRequested)
        {
            // Idempotent re-pin: same context, no pivots ⇒ keep position.
            return new PinIntentOutcome.Pinned(IntentRowMapper.ToDomain(intent), IntentPinRowMapper.ToDomain(existing), Created: false);
        }

        string newKey;
        if (reorderRequested)
        {
            newKey = FractionalIndex.Between(beforeKey, afterKey);
        }
        else
        {
            var maxKey = await GetTailKeyAsync(ctx, contextTagId, ct);
            newKey = FractionalIndex.Between(maxKey, after: null);
        }

        if (existing is not null)
        {
            existing.PinSortKey = newKey;
            await ctx.SaveChangesAsync(ct);
            var pin = new IntentPin(intentId, contextTagId, newKey, existing.CreatedAt);
            return new PinIntentOutcome.Pinned(IntentRowMapper.ToDomain(intent), pin, Created: false);
        }

        var row = new IntentPinRow
        {
            Id = Guid.NewGuid().ToString("N"),
            IntentId = intentId.Value,
            ContextTagId = contextTagId.Value,
            PinSortKey = newKey,
            CreatedAt = now,
        };
        ctx.Set<IntentPinRow>().Add(row);
        await ctx.SaveChangesAsync(ct);
        var created = new IntentPin(intentId, contextTagId, newKey, now);
        return new PinIntentOutcome.Pinned(IntentRowMapper.ToDomain(intent), created, Created: true);
    }

    public async Task<UnpinIntentOutcome> UnpinAsync(
        IntentId intentId,
        TagId contextTagId,
        CancellationToken ct)
    {
        var ctx = RequireWriteContext(nameof(UnpinAsync));
        var intent = await LoadIntentAsync(ctx, intentId, ct);
        if (intent is null)
        {
            return new UnpinIntentOutcome.IntentNotFound();
        }

        var intentWire = intentId.Value;
        var contextWire = contextTagId.Value;
        var affected = await ctx.Set<IntentPinRow>()
            .Where(r => r.IntentId == intentWire && r.ContextTagId == contextWire)
            .ExecuteDeleteAsync(ct);
        return new UnpinIntentOutcome.Unpinned(IntentRowMapper.ToDomain(intent), contextWire, Removed: affected > 0);
    }

    public async Task<MovePinOutcome> MoveAsync(
        IntentId intentId,
        TagId contextTagId,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            throw new ArgumentException("At least one of beforeId / afterId must be supplied.", nameof(beforeId));
        }

        var ctx = RequireWriteContext(nameof(MoveAsync));
        var intent = await LoadIntentAsync(ctx, intentId, ct);
        if (intent is null)
        {
            return new MovePinOutcome.IntentNotFound();
        }

        var existing = await FindExistingPinAsync(ctx, intentId, contextTagId, ct);
        if (existing is null)
        {
            return new MovePinOutcome.PinNotFound();
        }

        var (beforeKey, afterKey, pivotMissing) = await ResolvePivotKeysAsync(ctx, contextTagId, beforeId, afterId, ct);
        if (pivotMissing is not null)
        {
            return new MovePinOutcome.PivotNotFound(pivotMissing);
        }

        var newKey = FractionalIndex.Between(beforeKey, afterKey);
        if (string.Equals(existing.PinSortKey, newKey, StringComparison.Ordinal))
        {
            var stable = new IntentPin(intentId, contextTagId, newKey, existing.CreatedAt);
            return new MovePinOutcome.Moved(IntentRowMapper.ToDomain(intent), stable, Changed: false);
        }

        existing.PinSortKey = newKey;
        await ctx.SaveChangesAsync(ct);
        var pin = new IntentPin(intentId, contextTagId, newKey, existing.CreatedAt);
        return new MovePinOutcome.Moved(IntentRowMapper.ToDomain(intent), pin, Changed: true);
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>> GetPinnedInAsync(
        IReadOnlyList<string> intentIds,
        CancellationToken ct) =>
        ReadAsync<IReadOnlyDictionary<string, IReadOnlyList<IntentPin>>>(async (ctx, c) =>
        {
            var result = new Dictionary<string, IReadOnlyList<IntentPin>>(StringComparer.Ordinal);
            if (intentIds is null || intentIds.Count == 0)
            {
                return result;
            }
            var ids = intentIds.ToList();
            var rows = await ctx.Set<IntentPinRow>()
                .Where(r => ids.Contains(r.IntentId))
                .ToListAsync(c);
            foreach (var row in rows)
            {
                if (!result.TryGetValue(row.IntentId, out var list))
                {
                    list = new List<IntentPin>();
                    result[row.IntentId] = list;
                }
                ((List<IntentPin>)list).Add(IntentPinRowMapper.ToDomain(row));
            }
            return result;
        }, ct);

    public Task<IReadOnlyList<IntentPin>> ListAllAsync(CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentPin>>(async (ctx, c) =>
        {
            var rows = await ctx.Set<IntentPinRow>()
                .OrderBy(r => r.ContextTagId)
                .ThenBy(r => r.PinSortKey)
                .ToListAsync(c);
            return rows.Select(IntentPinRowMapper.ToDomain).ToList();
        }, ct);

    private static Task<IntentRow?> LoadIntentAsync(ThroneDbContext ctx, IntentId intentId, CancellationToken ct)
    {
        var id = intentId.Value;
        return ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    private static Task<bool> TagExistsAsync(ThroneDbContext ctx, string tagId, CancellationToken ct)
    {
        // Hand-rolled probe because the Tag aggregate's row type is owned by a later slice;
        // the column shape (id) is a stable schema contract. SqlQuery (interpolated) makes
        // `tagId` a SQL parameter while the table name stays a compile-time literal.
        return ctx.Database
            .SqlQuery<int>($"SELECT 1 AS Value FROM tags WHERE id = {tagId}")
            .AnyAsync(ct);
    }

    private static Task<IntentPinRow?> FindExistingPinAsync(
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

    private static async Task<(string? BeforeKey, string? AfterKey, string? Missing)> ResolvePivotKeysAsync(
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

    private static async Task<string?> GetTailKeyAsync(
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

    private ThroneDbContext RequireWriteContext(string method) =>
        Sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentPinRepository.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
