using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Intents;

/// <summary>
/// Tags + cleanup-on-done mutations. Pulled out of <see cref="EfIntentStatusMutator"/>
/// so neither type bumps into the per-type LOC budget; the clusters share the same
/// CAS pattern but operate on disjoint metadata columns.
/// </summary>
internal sealed class EfIntentTagsMutator(EfSessionAccessor sessions)
{
    public async Task<SetIntentTagsOutcome> SetTagsAsync(
        IntentId id,
        int expectedVersion,
        IReadOnlyList<TagId> tagIds,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tagIds);

        var ctx = RequireContext(nameof(SetTagsAsync));
        var wire = id.Value;

        var row = await ctx.Set<IntentRow>().FirstOrDefaultAsync(r => r.Id == wire, ct);
        if (row is null)
        {
            return new SetIntentTagsOutcome.NotFound();
        }
        if (row.CurrentVersion != expectedVersion)
        {
            return new SetIntentTagsOutcome.VersionConflict(row.CurrentVersion);
        }
        ctx.Entry(row).State = EntityState.Detached;

        var intent = IntentRowMapper.ToDomain(row);
        var oldTagIds = row.TagIds;
        var changed = intent.SetTags(tagIds, now);
        if (!changed)
        {
            return new SetIntentTagsOutcome.Updated(intent, Changed: false);
        }

        var newTagIds = intent.TagIds.Select(t => t.Value).ToList();
        var newUpdatedAt = intent.State.UpdatedAt;

        var affected = await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire && r.CurrentVersion == expectedVersion)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.TagIds, newTagIds)
                .SetProperty(r => r.UpdatedAt, newUpdatedAt), ct);

        if (affected == 0)
        {
            var fresh = await ctx.Set<IntentRow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, ct);
            return fresh is null
                ? new SetIntentTagsOutcome.NotFound()
                : new SetIntentTagsOutcome.VersionConflict(fresh.CurrentVersion);
        }

        var added = newTagIds.Where(t => !oldTagIds.Contains(t)).ToList();
        await EfTagAttachmentToucher.TouchAsync(ctx, added, now, ct);

        return new SetIntentTagsOutcome.Updated(intent, Changed: true);
    }

    public async Task SetCleanupLocalStateOnDoneAsync(
        IntentId id,
        bool value,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ctx = RequireContext(nameof(SetCleanupLocalStateOnDoneAsync));
        var wire = id.Value;
        await ctx.Set<IntentRow>()
            .Where(r => r.Id == wire)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.CleanupLocalStateOnDone, value)
                .SetProperty(r => r.UpdatedAt, now), ct);
    }

    private ThroneDbContext RequireContext(string method) =>
        sessions.Current
            ?? throw new InvalidOperationException(
                $"EfIntentTagsMutator.{method} must run inside IUnitOfWork.ExecuteAsync.");
}
