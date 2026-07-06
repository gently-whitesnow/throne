namespace Throne.Application.TaskTrackers;

/// <summary>
/// A task-tracker call failed in a way that classifies the connection's health. The adapter raises it
/// so a provider-neutral caller — a card pull, a re-probe — can branch on <see cref="Health"/> (record
/// the connection status, degrade the attachment) without seeing any adapter-internal exception type.
/// The <see cref="TaskTrackerConnectionHealth.Connected"/> value never appears here: success is not an
/// exception.
/// </summary>
public sealed class TaskTrackerConnectionException(TaskTrackerConnectionHealth health, string message)
    : Exception(message)
{
    public TaskTrackerConnectionHealth Health { get; } = health;
}
