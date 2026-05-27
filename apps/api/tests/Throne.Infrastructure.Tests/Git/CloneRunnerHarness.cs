using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Тестовый стенд для <see cref="RepositoryCloneService"/>: реальный канал /
/// workflow / recovery через <c>ServiceScope</c>, фейковый
/// <see cref="Throne.Application.Git.IGitProvider"/>, каждый «клон» висит на
/// персональном <see cref="TaskCompletionSource"/>. Это даёт детерминированный
/// замер in-flight slot'ов без сна и без spawn'а реальных процессов.
/// </summary>
internal sealed class CloneRunnerHarness : IAsyncDisposable
{
    private static readonly TimeSpan WaitBudget = TimeSpan.FromSeconds(5);

    private readonly RepositoryCloneService _service;
    private readonly CancellationTokenSource _cts = new();
    private readonly CloneInFlightTracker _tracker;

    private CloneRunnerHarness(RepositoryCloneService service, CloneInFlightTracker tracker)
    {
        _service = service;
        _tracker = tracker;
    }

    public int MaxObservedConcurrency => _tracker.MaxObserved;

    public static async Task<CloneRunnerHarness> StartAsync(int maxParallel, int pendingCount)
    {
        var tracker = new CloneInFlightTracker();
        var stand = CloneRunnerHarnessBuilder.Build(maxParallel, tracker);

        foreach (var binding in CloneRunnerHarnessBuilder.MakePendingBindings(pendingCount))
        {
            stand.Bindings.Seed(binding);
            await stand.Channel.EnqueueAsync(binding.Id, CancellationToken.None);
        }

        var harness = new CloneRunnerHarness(stand.Service, tracker);
        await harness._service.StartAsync(harness._cts.Token);
        return harness;
    }

    public Task WaitForInFlightAsync(int expected) => _tracker.WaitForInFlightAsync(expected, WaitBudget);

    public Task WaitForAllCompletedAsync() => _tracker.WaitForAllCompletedAsync(WaitBudget);

    public Task ReleaseOneAsync() => _tracker.ReleaseOneAsync(WaitBudget);

    public void ReleaseRemaining() => _tracker.ReleaseRemaining();

    public async ValueTask DisposeAsync()
    {
        // Освободить ещё открытые gates, чтобы Task.WhenAll внутри
        // CloneRunnerLoop.DrainAsync не висел на провисших клонах.
        _tracker.ReleaseRemaining();
        _cts.Cancel();
        try
        {
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _service.StopAsync(shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        _cts.Dispose();
    }
}
