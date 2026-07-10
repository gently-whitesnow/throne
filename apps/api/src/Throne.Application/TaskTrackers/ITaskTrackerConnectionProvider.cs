namespace Throne.Application.TaskTrackers;

/// <summary>
/// Connection-capable extension of <see cref="ITaskTrackerProvider"/> (ADR-0045): the behaviour the
/// settings axis needs to stand up a connection — validate credentials and read the board topology —
/// without the settings code touching any adapter-internal type. A provider that only carries catalog
/// identity does not implement this; the settings boundary resolves the registry entry and checks for
/// this capability, treating its absence as «tracker has no connectable surface».
/// </summary>
public interface ITaskTrackerConnectionProvider : ITaskTrackerProvider
{
    /// <summary>
    /// Validate <paramref name="connection"/> against the tracker API. Never throws on an
    /// authentication or transport failure — those are reported as the classified
    /// <see cref="TaskTrackerConnectionHealth"/> (auth / offline / blocked) so the caller can echo the
    /// state to the operator and decide whether to persist.
    /// </summary>
    Task<TaskTrackerProbeResult> ProbeAsync(TaskTrackerConnectionDescriptor connection, CancellationToken ct);

    /// <summary>
    /// Read the live space/board topology for board selection. Unlike <see cref="ProbeAsync"/> this is
    /// called against an already-saved connection, so an upstream failure is exceptional — it surfaces
    /// as an <see cref="Throne.Application.Errors.ApiException"/> (upstream-unavailable, 502).
    /// </summary>
    Task<IReadOnlyList<TaskTrackerSpaceTopology>> ListBoardsAsync(
        TaskTrackerConnectionDescriptor connection,
        CancellationToken ct);

    /// <summary>
    /// List the active cards visible on <paramref name="boardId"/> (all columns, archived excluded) for
    /// the card-browser read surface. No server-side search or pagination in this MVP — the provider
    /// aggregates the board's live cards and returns them whole. Every failure throws a
    /// <see cref="TaskTrackerConnectionException"/> carrying the classified
    /// <see cref="TaskTrackerConnectionHealth"/> (auth on 401/403, blocked on 402, offline on
    /// 5xx / transport / timeout), so the caller maps it onto the ADR-0053 degradation surface exactly
    /// as it does for <see cref="GetCardAsync"/>.
    /// </summary>
    Task<IReadOnlyList<TaskTrackerCard>> ListBoardCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        CancellationToken ct);

    /// <summary>
    /// Look up cards inside a board for the attach-card combobox: empty <paramref name="query"/> returns
    /// the most recently touched cards (top-N by updated-at desc), a non-empty one narrows by the
    /// tracker's own text filter. Bounded by <paramref name="limit"/>, archived cards excluded, no
    /// server-side paging beyond the first page — the surface is picker latency, not exhaustive listing.
    /// Failures follow the same taxonomy as <see cref="ListBoardCardsAsync"/>.
    /// </summary>
    Task<IReadOnlyList<TaskTrackerCard>> SearchCardsAsync(
        TaskTrackerConnectionDescriptor connection,
        string boardId,
        string? query,
        int limit,
        CancellationToken ct);

    /// <summary>
    /// Pull a single card snapshot by its provider-native id. Returns <see langword="null"/> only when
    /// the card is genuinely gone upstream (404) so the caller can record «gone» without branching on an
    /// exception. Every other failure throws a <see cref="TaskTrackerConnectionException"/> carrying the
    /// classified <see cref="TaskTrackerConnectionHealth"/> (auth on 401/403, blocked on 402, offline on
    /// 5xx / transport / timeout) — a forbidden card is a credentials problem, not an absent card, so it
    /// must not masquerade as «gone».
    /// </summary>
    Task<TaskTrackerCard?> GetCardAsync(
        TaskTrackerConnectionDescriptor connection,
        string cardId,
        CancellationToken ct);

    /// <summary>
    /// Build a browser-facing URL for the card identified by <paramref name="cardId"/> under the given
    /// connection, or <see langword="null"/> when this provider cannot compose one from the fields it has
    /// (unknown coordinate shape, missing base URL, tracker with no stable card URL). Returns a short
    /// canonical form the tracker itself redirects to a human-readable route — the caller does not depend
    /// on prefix/space metadata that is not part of the coordinate.
    /// </summary>
    string? BuildCardWebUrl(TaskTrackerConnectionDescriptor connection, string cardId);
}
