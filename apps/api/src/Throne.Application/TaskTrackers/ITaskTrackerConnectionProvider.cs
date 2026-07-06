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
}
