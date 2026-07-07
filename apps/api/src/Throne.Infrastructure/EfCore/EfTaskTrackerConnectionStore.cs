using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="ITaskTrackerConnectionStore"/>. Throne is local-first and single-operator
/// (ADR-0029), so the API token is persisted as-is alongside the base URL — no at-rest encryption.
/// Like the GitLab-host provider this is a settings store, not a domain repository — it uses one-shot
/// contexts and lives outside any unit of work.
/// </summary>
internal sealed class EfTaskTrackerConnectionStore(
    IDbContextFactory<ThroneDbContext> contextFactory)
    : ITaskTrackerConnectionStore
{
    public async Task<TaskTrackerStoredConnection?> GetAsync(string tracker, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerConnectionRow>().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Tracker == tracker, ct);
        if (row is null)
        {
            return null;
        }

        return new TaskTrackerStoredConnection(
            row.BaseUrl,
            row.Token,
            row.SelectedBoards.Select(ToSelection).ToList(),
            ParseStatus(row.LastStatus),
            row.LastError,
            row.LastCheckedAt);
    }

    public async Task SaveConnectionAsync(string tracker, string baseUrl, string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerConnectionRow>()
            .FirstOrDefaultAsync(r => r.Tracker == tracker, ct);
        if (row is null)
        {
            context.Set<TaskTrackerConnectionRow>().Add(new TaskTrackerConnectionRow
            {
                Tracker = tracker,
                BaseUrl = baseUrl,
                Token = token,
            });
        }
        else
        {
            row.BaseUrl = baseUrl;
            row.Token = token;
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string tracker, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        await context.Set<TaskTrackerConnectionRow>()
            .Where(r => r.Tracker == tracker)
            .ExecuteDeleteAsync(ct);
    }

    public async Task SaveHealthAsync(
        string tracker,
        TaskTrackerConnectionHealth status,
        string? detail,
        DateTimeOffset checkedAt,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerConnectionRow>()
            .FirstOrDefaultAsync(r => r.Tracker == tracker, ct);
        if (row is null)
        {
            return;
        }

        row.LastStatus = status.ToString();
        row.LastError = status == TaskTrackerConnectionHealth.Connected ? null : detail;
        row.LastCheckedAt = checkedAt;
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveSelectionAsync(
        string tracker,
        IReadOnlyList<TaskTrackerBoardSelection> selection,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentNullException.ThrowIfNull(selection);

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerConnectionRow>()
            .FirstOrDefaultAsync(r => r.Tracker == tracker, ct);
        if (row is null)
        {
            return;
        }

        row.SelectedBoards = selection.Select(ToRow).ToList();
        await context.SaveChangesAsync(ct);
    }

    private static TaskTrackerConnectionHealth? ParseStatus(string? stored) =>
        Enum.TryParse<TaskTrackerConnectionHealth>(stored, out var status) ? status : null;

    private static TaskTrackerBoardSelection ToSelection(TaskTrackerBoardSelectionRow row) =>
        new(row.SpaceId, row.SpaceTitle, row.BoardId, row.BoardTitle, row.ContextField);

    private static TaskTrackerBoardSelectionRow ToRow(TaskTrackerBoardSelection selection) => new()
    {
        SpaceId = selection.SpaceId,
        SpaceTitle = selection.SpaceTitle,
        BoardId = selection.BoardId,
        BoardTitle = selection.BoardTitle,
        ContextField = selection.ContextField,
    };
}
