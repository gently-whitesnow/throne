using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;

namespace Throne.Infrastructure.TaskTrackers;

/// <summary>
/// Background heartbeat that re-probes every saved task-tracker connection on a slow cadence and records
/// the outcome via <see cref="ITaskTrackerConnectionStore.SaveHealthAsync"/>, so the settings list
/// reflects reality even when the operator never opens a card. A probe never mutates the binding — an
/// offline tracker just keeps its credentials and reports «offline»; only auth surfaces «reconnect».
/// </summary>
internal sealed partial class TaskTrackerHealthProbeService(
    ITaskTrackerProviderRegistry registry,
    ITaskTrackerConnectionStore store,
    TimeProvider clock,
    IOptions<TaskTrackerHealthProbeOptions> options,
    ILogger<TaskTrackerHealthProbeService> log) : BackgroundService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "TaskTrackerHealthProbeService disabled: poll_interval <= 0.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "TaskTrackerHealthProbeService tick failed.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (settings.PollInterval <= TimeSpan.Zero)
        {
            LogDisabled(log);
            return;
        }

        using var timer = new PeriodicTimer(settings.PollInterval);
        try
        {
            do
            {
                try
                {
                    await ProbeAllAsync(registry, store, clock, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    LogTickFailed(log, ex);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
    }

    /// <summary>
    /// Probe each connectable provider that has a saved connection and persist its health. Static and
    /// dependency-light so the loop body is unit-testable without the hosted rig.
    /// </summary>
    public static async Task ProbeAllAsync(
        ITaskTrackerProviderRegistry registry,
        ITaskTrackerConnectionStore store,
        TimeProvider clock,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);

        foreach (var provider in registry.AllProviders.OfType<ITaskTrackerConnectionProvider>())
        {
            ct.ThrowIfCancellationRequested();
            var stored = await store.GetAsync(provider.TrackerKey, ct);
            if (stored is null)
            {
                continue;
            }

            var descriptor = new TaskTrackerConnectionDescriptor(stored.BaseUrl, stored.Token);
            var probe = await provider.ProbeAsync(descriptor, ct);
            await store.SaveHealthAsync(provider.TrackerKey, probe.Health, probe.Error, clock.GetUtcNow(), ct);
        }
    }
}
