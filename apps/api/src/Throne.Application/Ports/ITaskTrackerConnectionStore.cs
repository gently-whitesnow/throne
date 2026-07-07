using Throne.Application.TaskTrackers;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for a task-tracker connection: the workspace base URL, the API token, the selected
/// boards and the last observed connection health. One connection per tracker key. Throne is local-first
/// / single-operator (ADR-0029), so the store persists the token as-is — at-rest secret protection is
/// out of scope for this axis.
/// </summary>
public interface ITaskTrackerConnectionStore
{
    /// <summary>Read the saved connection for <paramref name="tracker"/>, or null when none exists.</summary>
    Task<TaskTrackerStoredConnection?> GetAsync(string tracker, CancellationToken ct);

    /// <summary>
    /// Upsert the credentials for <paramref name="tracker"/>. Preserves any existing board selection —
    /// re-issuing the connection does not silently drop the operator's board choices.
    /// </summary>
    Task SaveConnectionAsync(string tracker, string baseUrl, string token, CancellationToken ct);

    /// <summary>Remove the connection and its board selection. Idempotent.</summary>
    Task DeleteAsync(string tracker, CancellationToken ct);

    /// <summary>
    /// Record the last observed connection health. No-op when the connection is absent — health is a
    /// property of a saved connection, never of a phantom one. Called from the upsert, the background
    /// re-probe, and a card attach/refresh pull so the settings list never shows stale-green.
    /// </summary>
    Task SaveHealthAsync(
        string tracker,
        TaskTrackerConnectionHealth status,
        string? detail,
        DateTimeOffset checkedAt,
        CancellationToken ct);

    /// <summary>
    /// Replace the board selection for an existing connection. No-op when the connection is absent —
    /// the caller is expected to have rejected that case (409) before reaching here.
    /// </summary>
    Task SaveSelectionAsync(
        string tracker,
        IReadOnlyList<TaskTrackerBoardSelection> selection,
        CancellationToken ct);
}

/// <summary>
/// A persisted connection: credentials, the operator's board selection, and the last observed health.
/// <see cref="LastStatus"/> is null when the connection has not been probed since it was saved — the
/// caller treats that as a not-yet-observed «connected» baseline.
/// </summary>
public sealed record TaskTrackerStoredConnection(
    string BaseUrl,
    string Token,
    IReadOnlyList<TaskTrackerBoardSelection> Selection,
    TaskTrackerConnectionHealth? LastStatus = null,
    string? LastError = null,
    DateTimeOffset? LastCheckedAt = null);

/// <summary>
/// One selected board with its grouping context. Titles are cached so a downstream sync can name the
/// board without re-reading the topology; <see cref="ContextField"/> is the wire token
/// (<c>lane</c> / <c>tags</c> / <c>type</c> / <c>none</c>).
/// </summary>
public sealed record TaskTrackerBoardSelection(
    string SpaceId,
    string? SpaceTitle,
    string BoardId,
    string? BoardTitle,
    string ContextField);
