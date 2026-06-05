using System.Collections.Concurrent;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Детерминированный учёт in-flight «клонов»: каждый клон висит на персональном
/// <see cref="TaskCompletionSource"/>-гейте, а ожидание условий разрешается сигналом
/// ожидающим при каждом изменении состояния (см. <see cref="CloneConditionWaiters"/>),
/// а не polling-сном — чтобы не плодить скрытую flaky-поверхность в тестах runner'а.
/// Сценарные хелперы ожидания/освобождения — в <see cref="CloneInFlightWaits"/>.
/// </summary>
internal sealed class CloneInFlightTracker
{
    private readonly ConcurrentBag<TaskCompletionSource> _gates = new();
    private readonly Lock _stateLock = new();
    private readonly CloneConditionWaiters _waiters;
    private int _inFlight;
    private int _completed;
    private int _maxObserved;

    public CloneInFlightTracker()
    {
        _waiters = new CloneConditionWaiters(_stateLock, SnapshotLocked);
    }

    public int InFlight
    {
        get { lock (_stateLock) { return _inFlight; } }
    }

    public int Completed
    {
        get { lock (_stateLock) { return _completed; } }
    }

    public int MaxObserved
    {
        get { lock (_stateLock) { return _maxObserved; } }
    }

    public Task BeginAsync()
    {
        // Гейт добавляется до инкремента, поэтому к моменту сигнала (in-flight вырос)
        // соответствующий gate уже доступен для release.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gates.Add(gate);
        lock (_stateLock)
        {
            _maxObserved = Math.Max(_maxObserved, ++_inFlight);
        }
        _waiters.Signal();
        return gate.Task.ContinueWith(Complete, TaskContinuationOptions.ExecuteSynchronously);
    }

    public Task WaitForAsync(Func<CloneSnapshot, bool> satisfied, TimeSpan budget, Func<string> onTimeout) =>
        _waiters.WaitAsync(satisfied, budget, onTimeout);

    public bool TryRelease()
    {
        var taken = _gates.TryTake(out var gate);
        gate?.TrySetResult();
        return taken;
    }

    private void Complete(Task _)
    {
        lock (_stateLock)
        {
            _inFlight--;
            _completed++;
        }
        _waiters.Signal();
    }

    private CloneSnapshot SnapshotLocked() => new(_inFlight, _completed, !_gates.IsEmpty);
}
