using System.Collections.Concurrent;
using FluentAssertions;

namespace Throne.Infrastructure.Tests.Git;

internal sealed class CloneInFlightTracker
{
    private readonly ConcurrentBag<TaskCompletionSource> _gates = new();
    private readonly Lock _maxLock = new();
    private int _inFlight;
    private int _completed;
    private int _maxObserved;

    public int InFlight => Volatile.Read(ref _inFlight);
    public int Completed => Volatile.Read(ref _completed);

    public int MaxObserved
    {
        get
        {
            lock (_maxLock)
            {
                return _maxObserved;
            }
        }
    }

    public Task BeginAsync()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _gates.Add(gate);
        UpdateMax(Interlocked.Increment(ref _inFlight));
        return gate.Task.ContinueWith(
            _ =>
            {
                Interlocked.Decrement(ref _inFlight);
                Interlocked.Increment(ref _completed);
            },
            TaskContinuationOptions.ExecuteSynchronously);
    }

    public bool TryTakeGate(out TaskCompletionSource gate) => _gates.TryTake(out gate!);

    private void UpdateMax(int current)
    {
        lock (_maxLock)
        {
            if (current > _maxObserved)
            {
                _maxObserved = current;
            }
        }
    }
}

internal static class CloneInFlightReleases
{
    public static async Task ReleaseOneAsync(this CloneInFlightTracker tracker, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (DateTime.UtcNow < deadline)
        {
            if (tracker.TryTakeGate(out var gate))
            {
                gate.TrySetResult();
                return;
            }
            await Task.Delay(10);
        }
        throw new InvalidOperationException("Нет ожидающих клонов для release (timeout).");
    }

    public static void ReleaseRemaining(this CloneInFlightTracker tracker)
    {
        while (tracker.TryTakeGate(out var gate))
        {
            gate.TrySetResult();
        }
    }
}

internal static class CloneInFlightWaits
{
    public static async Task WaitForInFlightAsync(this CloneInFlightTracker tracker, int expected, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (tracker.InFlight < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        tracker.InFlight.Should().BeGreaterOrEqualTo(expected,
            $"runner должен был поднять минимум {expected} клонов в параллель");
    }

    public static async Task WaitForCompletedAsync(this CloneInFlightTracker tracker, int expected, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (tracker.Completed < expected && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        tracker.Completed.Should().BeGreaterOrEqualTo(expected,
            $"runner должен был завершить минимум {expected} клонов");
    }

    public static async Task WaitForAllCompletedAsync(this CloneInFlightTracker tracker, TimeSpan budget)
    {
        var deadline = DateTime.UtcNow + budget;
        while (tracker.InFlight > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        tracker.InFlight.Should().Be(0);
    }
}
