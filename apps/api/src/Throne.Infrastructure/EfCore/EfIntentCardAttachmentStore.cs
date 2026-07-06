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
/// <see cref="EfRepositoryBase.RequireContext"/>. Upsert is «update-by-id, else insert» so a re-attach
/// overwrites the snapshot without a second identity.
/// </summary>
internal sealed class EfIntentCardAttachmentStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentCardAttachmentStore
{
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

    public async Task UpsertAsync(IntentCardAttachment attachment, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        var ctx = RequireContext();

        var id = attachment.Id.Value;
        var state = attachment.State;
        var snapshot = state.Snapshot;

        var affected = await ctx.Set<IntentCardAttachmentRow>()
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

        if (affected == 0)
        {
            ctx.Set<IntentCardAttachmentRow>().Add(IntentCardAttachmentRowMapper.ToRow(attachment));
            await ctx.SaveChangesAsync(ct);
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
}
