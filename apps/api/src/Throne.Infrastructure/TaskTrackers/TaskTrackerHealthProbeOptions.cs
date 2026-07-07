namespace Throne.Infrastructure.TaskTrackers;

/// <summary>
/// Cadence for the background connection re-probe. Bound from <c>Throne:TaskTrackers:HealthProbe</c>.
/// The default is deliberately slow — this is a «is the saved connection still alive?» heartbeat, not a
/// sync loop; card attach/refresh already records health on real use. A non-positive
/// <see cref="PollInterval"/> disables the loop entirely (health then updates only on upsert / card pull).
/// </summary>
public sealed class TaskTrackerHealthProbeOptions
{
    public const string SectionName = "Throne:TaskTrackers:HealthProbe";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);
}
