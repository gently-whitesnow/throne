using System.Collections.Concurrent;

namespace Throne.Application.TaskTrackers.Sync;

/// <summary>
/// Per-board exponential backoff for the poll loop, mirroring <c>PullRequestSyncBackoff</c>. State is
/// in-process; the worst case after a restart is one extra poll attempt against a still-failing board.
/// </summary>
public sealed class TaskTrackerSyncBackoff(TaskTrackerSyncOptions options)
{
    private readonly ConcurrentDictionary<string, BackoffState> _state = new(StringComparer.Ordinal);

    public bool ShouldSkip(string key, DateTimeOffset now) =>
        _state.TryGetValue(key, out var state) && state.NotBefore > now;

    public void RecordSuccess(string key) => _state.TryRemove(key, out _);

    public void RecordFailure(string key, DateTimeOffset now)
    {
        var initial = Math.Max(1, options.BackoffInitialSeconds);
        var max = Math.Max(initial, options.BackoffMaxSeconds);
        _state.AddOrUpdate(
            key,
            _ => new BackoffState(now.AddSeconds(initial), initial),
            (_, prev) =>
            {
                var next = Math.Min(max, prev.LastDelaySeconds * 2);
                return new BackoffState(now.AddSeconds(next), next);
            });
    }

    private readonly record struct BackoffState(DateTimeOffset NotBefore, int LastDelaySeconds);
}
