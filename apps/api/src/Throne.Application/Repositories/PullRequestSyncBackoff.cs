using System.Collections.Concurrent;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Per-binding exponential-backoff tracker for the polling loop (T-10). State lives
/// in-process — the worst case of a process restart is one extra poll attempt against
/// upstream, which is bounded by the rate-limit guard inside the provider.
///
/// <list type="bullet">
///   <item><see cref="ShouldSkip"/> returns <see langword="true"/> if the binding is
///         still cooling down from a prior failure.</item>
///   <item><see cref="RecordSuccess"/> clears the backoff so the next tick polls the
///         binding again at the base interval.</item>
///   <item><see cref="RecordFailure"/> doubles the delay (capped at
///         <c>BackoffMaxSeconds</c>) and stores the «not before» wall-clock instant.</item>
/// </list>
/// </summary>
public sealed class PullRequestSyncBackoff(PullRequestSyncOptions options)
{
    private readonly PullRequestSyncOptions _opts = options;
    private readonly ConcurrentDictionary<string, BackoffState> _state = new(StringComparer.Ordinal);

    public bool ShouldSkip(BindingId id, DateTimeOffset now)
    {
        return _state.TryGetValue(id.Value, out var state) && state.NotBefore > now;
    }

    public void RecordSuccess(BindingId id)
    {
        _state.TryRemove(id.Value, out _);
    }

    public void RecordFailure(BindingId id, DateTimeOffset now)
    {
        var initial = Math.Max(1, _opts.BackoffInitialSeconds);
        var max = Math.Max(initial, _opts.BackoffMaxSeconds);
        _state.AddOrUpdate(
            id.Value,
            _ => new BackoffState(now.AddSeconds(initial), initial),
            (_, prev) =>
            {
                var next = Math.Min(max, prev.LastDelaySeconds * 2);
                return new BackoffState(now.AddSeconds(next), next);
            });
    }

    public void Forget(BindingId id)
    {
        _state.TryRemove(id.Value, out _);
    }

    private sealed record BackoffState(DateTimeOffset NotBefore, int LastDelaySeconds);
}
