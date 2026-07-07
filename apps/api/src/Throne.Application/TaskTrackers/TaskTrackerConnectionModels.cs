namespace Throne.Application.TaskTrackers;

/// <summary>
/// Provider-neutral credentials handed to a <see cref="ITaskTrackerConnectionProvider"/> for a single
/// probe or board read. Mirrors the adapter's per-call connection shape (base URL + token) without
/// leaking any adapter-internal type to the settings axis.
/// </summary>
public sealed record TaskTrackerConnectionDescriptor(string BaseUrl, string Token);

/// <summary>
/// Three-state health of a task-tracker connection. Only <see cref="Connected"/> is a healthy binding;
/// the failure states are kept distinct instead of collapsed into one «unreachable» so the operator
/// sees the right next action:
/// <list type="bullet">
///   <item><see cref="Offline"/> — no network / 5xx / timeout. The binding stays valid and the
///         connection is re-probed on a quiet backoff; state is never lost to «gone».</item>
///   <item><see cref="Auth"/> — 401/403. The token was rejected; the operator must reconnect.</item>
///   <item><see cref="Blocked"/> — 402. The tracker refused on tariff grounds; an operator plan action
///         is required. Deliberately not hidden inside <see cref="Offline"/>.</item>
/// </list>
/// (A vanished/forbidden card — «gone» — is a per-attachment availability, not a connection health;
/// see <c>CardAvailabilityNames</c>.)
/// </summary>
public enum TaskTrackerConnectionHealth
{
    Connected,
    Offline,
    Auth,
    Blocked,
}

/// <summary>Outcome of <see cref="ITaskTrackerConnectionProvider.ProbeAsync"/>.</summary>
public sealed record TaskTrackerProbeResult(TaskTrackerConnectionHealth Health, string? Error)
{
    public static TaskTrackerProbeResult Connected() => new(TaskTrackerConnectionHealth.Connected, null);

    public static TaskTrackerProbeResult Offline(string error) =>
        new(TaskTrackerConnectionHealth.Offline, error);

    public static TaskTrackerProbeResult Auth(string error) =>
        new(TaskTrackerConnectionHealth.Auth, error);

    public static TaskTrackerProbeResult Blocked(string error) =>
        new(TaskTrackerConnectionHealth.Blocked, error);

    public static TaskTrackerProbeResult FromHealth(TaskTrackerConnectionHealth health, string? error) =>
        new(health, health == TaskTrackerConnectionHealth.Connected ? null : error);
}

/// <summary>A board exposed by the tracker, identified by an opaque provider-native id.</summary>
public sealed record TaskTrackerBoardRef(string BoardId, string BoardTitle);

/// <summary>A space and the boards it contains — the topology unit returned for board selection.</summary>
public sealed record TaskTrackerSpaceTopology(
    string SpaceId,
    string SpaceTitle,
    IReadOnlyList<TaskTrackerBoardRef> Boards);

/// <summary>
/// A board flattened out of the space topology, carrying its space context. The unit the board catalog
/// caches and returns from a search — a board name alone is ambiguous, so the space disambiguates it.
/// </summary>
public sealed record TaskTrackerBoardMatch(
    string BoardId,
    string BoardTitle,
    string SpaceId,
    string SpaceTitle);
