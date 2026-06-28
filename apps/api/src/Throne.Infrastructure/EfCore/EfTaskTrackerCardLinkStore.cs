using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Domain.TaskTrackers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

internal sealed class EfTaskTrackerCardLinkStore(IDbContextFactory<ThroneDbContext> contextFactory)
    : ITaskTrackerCardLinkStore
{
    public async Task<CardSyncLink?> GetByIntentAsync(string intentId, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerCardLinkRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.IntentId == intentId, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<CardSyncLink?> GetByCardAsync(string tracker, string boardId, string cardId, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerCardLinkRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Tracker == tracker && r.BoardId == boardId && r.CardId == cardId, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<CardSyncLink>> ListByTrackerAsync(string tracker, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var rows = await context.Set<TaskTrackerCardLinkRow>().AsNoTracking()
            .Where(r => r.Tracker == tracker).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<CardSyncLink>> ListByBoardAsync(string tracker, string boardId, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var rows = await context.Set<TaskTrackerCardLinkRow>().AsNoTracking()
            .Where(r => r.Tracker == tracker && r.BoardId == boardId).ToListAsync(ct);
        return rows.Select(ToDomain).ToList();
    }

    public async Task SaveAsync(CardSyncLink link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var set = context.Set<TaskTrackerCardLinkRow>();
        var row = await set.FirstOrDefaultAsync(r => r.IntentId == link.IntentId, ct);
        if (row is null)
        {
            set.Add(ToRow(link));
        }
        else
        {
            Apply(row, link);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteByIntentAsync(string intentId, CancellationToken ct)
    {
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        await context.Set<TaskTrackerCardLinkRow>().Where(r => r.IntentId == intentId).ExecuteDeleteAsync(ct);
    }

    private static TaskTrackerCardLinkRow ToRow(CardSyncLink link)
    {
        var row = new TaskTrackerCardLinkRow { IntentId = link.IntentId };
        Apply(row, link);
        row.CreatedAt = link.CreatedAt;
        return row;
    }

    private static void Apply(TaskTrackerCardLinkRow row, CardSyncLink link)
    {
        row.Tracker = link.Card.Tracker;
        row.BoardId = link.Card.BoardId;
        row.CardId = link.Card.CardId;
        row.ColumnId = link.Snapshot.ColumnId;
        row.ColumnTitle = link.Snapshot.ColumnTitle;
        row.SnapshotTitle = link.Snapshot.Title;
        row.SnapshotDescription = link.Snapshot.Description;
        row.CardUpdatedAt = link.Cursors.CardUpdatedAt;
        row.ColumnChangedAt = link.Cursors.ColumnChangedAt;
        row.LastSyncedAt = link.Cursors.LastSyncedAt;
        row.State = link.State;
        row.UpdatedAt = link.UpdatedAt;
    }

    private static CardSyncLink ToDomain(TaskTrackerCardLinkRow row) =>
        CardSyncLink.Restore(
            row.IntentId,
            new TaskTrackerCardLink(row.Tracker, row.BoardId, row.CardId),
            new CardSyncLinkSnapshot(row.SnapshotTitle, row.SnapshotDescription, row.ColumnId, row.ColumnTitle),
            new CardSyncLinkCursors(row.CardUpdatedAt, row.ColumnChangedAt, row.LastSyncedAt),
            row.State,
            row.CreatedAt,
            row.UpdatedAt);
}
