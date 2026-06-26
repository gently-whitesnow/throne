using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

internal sealed class EfIntentOrderingMutator(EfSessionAccessor sessions)
{
    public async Task<MoveIntentOutcome> MoveBetweenAsync(
        IntentId id,
        IntentId? beforeId,
        IntentId? afterId,
        CancellationToken ct)
    {
        if (beforeId is null && afterId is null)
        {
            throw new ArgumentException("At least one of beforeId / afterId must be supplied.", nameof(beforeId));
        }

        var ctx = RequireContext(nameof(MoveBetweenAsync));

        var (beforeKey, beforeMissing) = await ResolvePivotAsync(ctx, beforeId, ct);
        if (beforeMissing)
        {
            return new MoveIntentOutcome.PivotNotFound(beforeId!.Value.Value);
        }
        var (afterKey, afterMissing) = await ResolvePivotAsync(ctx, afterId, ct);
        if (afterMissing)
        {
            return new MoveIntentOutcome.PivotNotFound(afterId!.Value.Value);
        }

        var wire = id.Value;
        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new MoveIntentOutcome.NotFound();
        }

        var newSortKey = FractionalIndex.Between(beforeKey, afterKey);
        if (string.Equals(row.SortKey, newSortKey, StringComparison.Ordinal))
        {
            return new MoveIntentOutcome.Moved(IntentRowMapper.ToDomain(row), Changed: false);
        }

        ctx.Entry(row).State = EntityState.Detached;

        // Reorder is purely positional: do not touch updated_at or current_version.
        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.SortKey, newSortKey), ct);
        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new MoveIntentOutcome.NotFound()
                : new MoveIntentOutcome.Moved(IntentRowMapper.ToDomain(fresh), Changed: false);
        }

        row.SortKey = newSortKey;
        return new MoveIntentOutcome.Moved(IntentRowMapper.ToDomain(row), Changed: true);
    }

    private static async Task<(string? Key, bool Missing)> ResolvePivotAsync(
        ThroneDbContext ctx,
        IntentId? pivotId,
        CancellationToken ct)
    {
        if (pivotId is null)
        {
            return (null, false);
        }
        var wire = pivotId.Value.Value;
        var key = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire)
            .Select(r => r.SortKey)
            .FirstOrDefaultAsync(ct);
        return key is null ? (null, true) : (key, false);
    }

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentOrderingMutator.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
