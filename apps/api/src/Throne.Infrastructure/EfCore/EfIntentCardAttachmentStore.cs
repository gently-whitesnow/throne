using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.TaskTrackers;
using Throne.Infrastructure.EfCore.Mappers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="IIntentCardAttachmentStore"/> (ADR-0052). Reads run <c>AsNoTracking</c> on the
/// ambient session (or a transient context); writes require an ambient unit-of-work context via
/// <see cref="EfRepositoryBase.RequireContext"/>. Upsert is «update-by-id, else insert» with
/// unique-coordinate race recovery so a concurrent re-attach returns the already-created identity
/// instead of leaking a SQLite constraint error.
/// </summary>
internal sealed class EfIntentCardAttachmentStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentCardAttachmentStore
{
    private const int SqliteConstraintErrorCode = 19;
    private const int SqliteUniqueErrorCode = 2067;

    public Task<IReadOnlyList<IntentCardAttachment>> ListByIntentAsync(IntentId intentId, CancellationToken ct) =>
        ReadAsync<IReadOnlyList<IntentCardAttachment>>(async (ctx, c) =>
        {
            var wire = intentId.Value;
            var rows = await ctx.Set<IntentCardAttachmentRow>().AsNoTracking()
                .Where(r => r.IntentId == wire)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(c);
            return rows.Select(IntentCardAttachmentRowMapper.ToDomain).ToList();
        }, ct);

    public Task<IntentCardAttachment?> GetAsync(CardAttachmentId id, CancellationToken ct) =>
        ReadAsync(async (ctx, c) =>
        {
            var wire = id.Value;
            var row = await ctx.Set<IntentCardAttachmentRow>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == wire, c);
            return row is null ? null : IntentCardAttachmentRowMapper.ToDomain(row);
        }, ct);

    public Task<IntentCardAttachment?> GetByCoordinateAsync(
        IntentId intentId, CardCoordinate coordinate, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return ReadAsync(async (ctx, c) =>
        {
            var intent = intentId.Value;
            var tracker = coordinate.Tracker;
            var board = coordinate.BoardId;
            var card = coordinate.CardId;
            var row = await ctx.Set<IntentCardAttachmentRow>().AsNoTracking()
                .FirstOrDefaultAsync(
                    r => r.IntentId == intent && r.Tracker == tracker && r.BoardId == board && r.CardId == card,
                    c);
            return row is null ? null : IntentCardAttachmentRowMapper.ToDomain(row);
        }, ct);
    }

    public async Task<IntentCardAttachment> UpsertAsync(IntentCardAttachment attachment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var ctx = RequireContext();

        var id = attachment.Id.Value;
        var state = attachment.State;
        var affected = await UpdateMutableByIdAsync(ctx, id, state, ct);
        if (affected > 0)
        {
            return attachment;
        }

        ctx.Set<IntentCardAttachmentRow>().Add(IntentCardAttachmentRowMapper.ToRow(attachment));
        try
        {
            await ctx.SaveChangesAsync(ct);
            return attachment;
        }
        catch (DbUpdateException ex) when (IsUniqueConflict(ex))
        {
            DetachInserts(ctx);
            var existing = await FindByCoordinateAsync(ctx, attachment.IntentId, attachment.Coordinate, ct);
            if (existing is null)
            {
                throw;
            }

            var winnerId = existing.Id;
            var updateAffected = await UpdateMutableByIdAsync(ctx, winnerId, state, ct);
            if (updateAffected == 0)
            {
                throw new InvalidOperationException(
                    "Intent card attachment unique-coordinate winner disappeared before refresh.");
            }

            return WithExistingIdentity(attachment, existing);
        }
    }

    public async Task<bool> DeleteAsync(CardAttachmentId id, CancellationToken ct)
    {
        var ctx = RequireContext();
        var wire = id.Value;
        var affected = await ctx.Set<IntentCardAttachmentRow>()
            .Where(r => r.Id == wire)
            .ExecuteDeleteAsync(ct);
        return affected > 0;
    }

    private static Task<int> UpdateMutableByIdAsync(
        ThroneDbContext ctx,
        string id,
        IntentCardAttachmentState state,
        CancellationToken ct)
    {
        var snapshot = state.Snapshot;
        return ctx.Set<IntentCardAttachmentRow>()
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Title, snapshot.Title)
                .SetProperty(r => r.Description, snapshot.Description)
                .SetProperty(r => r.ColumnTitle, snapshot.ColumnTitle)
                .SetProperty(r => r.Archived, snapshot.Archived)
                .SetProperty(r => r.CardVersion, snapshot.CardVersion)
                .SetProperty(r => r.Availability, state.Availability)
                .SetProperty(r => r.FetchedAt, snapshot.FetchedAt)
                .SetProperty(r => r.UpdatedAt, state.UpdatedAt), ct);
    }

    private static Task<IntentCardAttachmentRow?> FindByCoordinateAsync(
        ThroneDbContext ctx,
        IntentId intentId,
        CardCoordinate coordinate,
        CancellationToken ct)
    {
        var intent = intentId.Value;
        var tracker = coordinate.Tracker;
        var board = coordinate.BoardId;
        var card = coordinate.CardId;
        return ctx.Set<IntentCardAttachmentRow>().AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.IntentId == intent && r.Tracker == tracker && r.BoardId == board && r.CardId == card,
                ct);
    }

    private static IntentCardAttachment WithExistingIdentity(
        IntentCardAttachment incoming,
        IntentCardAttachmentRow existing) =>
        IntentCardAttachment.Restore(new IntentCardAttachmentSnapshot(
            Id: new CardAttachmentId(existing.Id),
            IntentId: incoming.IntentId,
            Coordinate: incoming.Coordinate,
            Snapshot: incoming.State.Snapshot,
            Availability: incoming.State.Availability,
            CreatedAt: existing.CreatedAt,
            UpdatedAt: incoming.State.UpdatedAt));

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        var inner = ex.InnerException;
        while (inner is not null)
        {
            if (inner is SqliteException sqlite
                && (sqlite.SqliteErrorCode == SqliteConstraintErrorCode
                    || sqlite.SqliteExtendedErrorCode == SqliteUniqueErrorCode))
            {
                return true;
            }
            inner = inner.InnerException;
        }
        return false;
    }

    private static void DetachInserts(ThroneDbContext ctx)
    {
        foreach (var entry in ctx.ChangeTracker.Entries<IntentCardAttachmentRow>().ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
