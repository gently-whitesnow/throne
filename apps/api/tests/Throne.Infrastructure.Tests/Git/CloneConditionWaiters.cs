namespace Throne.Infrastructure.Tests.Git;

/// <summary>Снимок наблюдаемого состояния runner'а для предикатов ожидания.</summary>
internal readonly record struct CloneSnapshot(int InFlight, int Completed, bool HasGate);

/// <summary>
/// Набор ожидающих условие на состоянии, защищённом общим <see cref="Lock"/>.
/// Снимок читается под тем же замком, что и мутации владельца, поэтому проверка
/// условия и регистрация ожидающего атомарны относительно изменений — ожидание
/// разрешается событием (сигналом), а не polling-сном.
/// </summary>
internal sealed class CloneConditionWaiters(Lock gate, Func<CloneSnapshot> snapshotLocked)
{
    private readonly List<Waiter> _waiters = new();

    public async Task WaitAsync(Func<CloneSnapshot, bool> satisfied, TimeSpan budget, Func<string> onTimeout)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (satisfied(snapshotLocked()))
            {
                return;
            }
            _waiters.Add(new Waiter(satisfied, completion));
        }
        try
        {
            await completion.Task.WaitAsync(budget);
        }
        catch (TimeoutException)
        {
            lock (gate)
            {
                _waiters.RemoveAll(w => ReferenceEquals(w.Completion, completion));
            }
            throw new InvalidOperationException(onTimeout());
        }
    }

    public void Signal()
    {
        List<TaskCompletionSource>? ready = null;
        lock (gate)
        {
            var snapshot = snapshotLocked();
            for (var i = _waiters.Count - 1; i >= 0; i--)
            {
                if (!_waiters[i].Satisfied(snapshot))
                {
                    continue;
                }
                (ready ??= new List<TaskCompletionSource>()).Add(_waiters[i].Completion);
                _waiters.RemoveAt(i);
            }
        }
        if (ready is null)
        {
            return;
        }
        foreach (var completion in ready)
        {
            completion.TrySetResult();
        }
    }

    private sealed record Waiter(Func<CloneSnapshot, bool> Satisfied, TaskCompletionSource Completion);
}
