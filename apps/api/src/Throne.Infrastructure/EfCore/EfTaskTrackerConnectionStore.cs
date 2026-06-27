using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;
using Throne.Infrastructure.EfCore.Rows;
using Throne.Infrastructure.Security;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core <see cref="ITaskTrackerConnectionStore"/>. Encrypts the token on the way in and decrypts on
/// the way out via <see cref="ISecretProtector"/>, so the plaintext crosses only the in-process port
/// boundary and never the database file. Like the GitLab-host provider this is a settings store, not a
/// domain repository — it uses one-shot contexts and lives outside any unit of work.
/// </summary>
internal sealed class EfTaskTrackerConnectionStore(
    IDbContextFactory<ThroneDbContext> contextFactory,
    ISecretProtector protector)
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
            protector.Unprotect(row.EncryptedToken),
            row.SelectedBoards.Select(ToSelection).ToList());
    }

    public async Task SaveConnectionAsync(string tracker, string baseUrl, string token, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tracker);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        var row = await context.Set<TaskTrackerConnectionRow>()
            .FirstOrDefaultAsync(r => r.Tracker == tracker, ct);
        var encrypted = protector.Protect(token);
        if (row is null)
        {
            context.Set<TaskTrackerConnectionRow>().Add(new TaskTrackerConnectionRow
            {
                Tracker = tracker,
                BaseUrl = baseUrl,
                EncryptedToken = encrypted,
            });
        }
        else
        {
            row.BaseUrl = baseUrl;
            row.EncryptedToken = encrypted;
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
