namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Cadence + backoff for the task-tracker poll loop, mirroring <c>PullRequestSyncOptions</c>.
/// <see cref="PollIntervalSeconds"/> &lt;= 0 disables the background poll entirely (force-pull and
/// write-through still work).
/// </summary>
public sealed class TaskTrackerSyncOptions
{
    public const string SectionName = "Throne:TaskTrackerSync";

    public int PollIntervalSeconds { get; set; } = 120;

    public int BackoffInitialSeconds { get; set; } = 30;

    public int BackoffMaxSeconds { get; set; } = 900;
}
