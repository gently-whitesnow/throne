namespace Throne.Application.TaskTrackers;

/// <summary>
/// Provider-neutral credentials handed to a <see cref="ITaskTrackerConnectionProvider"/> for a single
/// probe or board read. Mirrors the adapter's per-call connection shape (base URL + token) without
/// leaking any adapter-internal type to the settings axis.
/// </summary>
public sealed record TaskTrackerConnectionDescriptor(string BaseUrl, string Token);

/// <summary>
/// Health of a credentials probe. <see cref="Connected"/> persists; <see cref="Invalid"/> (token
/// rejected) and <see cref="Unreachable"/> (transport/5xx) do not — they surface inline so the
/// operator can correct the input.
/// </summary>
public enum TaskTrackerConnectionHealth
{
    Connected,
    Invalid,
    Unreachable,
}

/// <summary>Outcome of <see cref="ITaskTrackerConnectionProvider.ProbeAsync"/>.</summary>
public sealed record TaskTrackerProbeResult(TaskTrackerConnectionHealth Health, string? Error)
{
    public static TaskTrackerProbeResult Connected() => new(TaskTrackerConnectionHealth.Connected, null);

    public static TaskTrackerProbeResult Invalid(string error) =>
        new(TaskTrackerConnectionHealth.Invalid, error);

    public static TaskTrackerProbeResult Unreachable(string error) =>
        new(TaskTrackerConnectionHealth.Unreachable, error);
}

/// <summary>A board exposed by the tracker, identified by an opaque provider-native id.</summary>
public sealed record TaskTrackerBoardRef(string BoardId, string BoardTitle);

/// <summary>A space and the boards it contains — the topology unit returned for board selection.</summary>
public sealed record TaskTrackerSpaceTopology(
    string SpaceId,
    string SpaceTitle,
    IReadOnlyList<TaskTrackerBoardRef> Boards);
