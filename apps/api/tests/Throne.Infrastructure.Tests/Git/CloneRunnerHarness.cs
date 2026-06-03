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

    internal CloneRunnerHarness(RepositoryCloneService service, CloneInFlightTracker tracker)
    {
        _service = service;
        _tracker = tracker;
    }

    public int MaxObservedConcurrency => _tracker.MaxObserved;

    public Task StartServiceAsync() => _service.StartAsync(_cts.Token);

    public Task WaitForInFlightAsync(int expected) => _tracker.WaitForInFlightAsync(expected, WaitBudget);

    public Task WaitForAllCompletedAsync() => _tracker.WaitForAllCompletedAsync(WaitBudget);

    public Task WaitForCompletedAsync(int expected) => _tracker.WaitForCompletedAsync(expected, WaitBudget);

    public Task ReleaseOneAsync() => _tracker.ReleaseOneAsync(WaitBudget);

    public void ReleaseRemaining() => _tracker.ReleaseRemaining();

    public async ValueTask DisposeAsync()
    {
        _tracker.ReleaseRemaining();
        _cts.Cancel();
        await CloneRunnerShutdown.StopAsync(_service);
        _cts.Dispose();
    }
}

internal static class CloneRunnerHarnessStarter
{
    public static async Task<CloneRunnerHarness> StartAsync(int maxParallel, int pendingCount)
    {
        var tracker = new CloneInFlightTracker();
        var stand = CloneRunnerHarnessBuilder.Build(maxParallel, tracker);
        await SeedAsync(stand, pendingCount);
        var harness = new CloneRunnerHarness(stand.Service, tracker);
        await harness.StartServiceAsync();
        return harness;
    }

    private static async Task SeedAsync(CloneRunnerStand stand, int pendingCount)
    {
        foreach (var binding in CloneRunnerHarnessBuilder.MakePendingBindings(pendingCount))
        {
            stand.Bindings.Seed(binding);
            await stand.Channel.EnqueueAsync(binding.Id, CancellationToken.None);
        }
    }
}

internal static class CloneRunnerShutdown
{
    public static async Task StopAsync(RepositoryCloneService service)
    {
        try
        {
            using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await service.StopAsync(shutdownCts.Token);
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }
}
