using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.TaskTrackers.Sync;

namespace Throne.Infrastructure.TaskTrackers.Sync;

/// <summary>
/// <see cref="BackgroundService"/> driving the task-tracker poll loop, mirroring
/// <c>PullRequestSyncService</c>: a <see cref="PeriodicTimer"/> whose per-tick orchestration lives in
/// the Application-layer <see cref="TaskTrackerSyncTickWorkflow"/> so it is testable without a hosted
/// rig. Tick failures are caught and logged so one bad iteration never stops the worker.
/// </summary>
internal sealed partial class TaskTrackerSyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<TaskTrackerSyncOptions> options,
    ILogger<TaskTrackerSyncService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        if (options.Value.PollIntervalSeconds <= 0)
        {
            LogDisabled(logger);
            return;
        }

        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                await RunTickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task RunTickAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<TaskTrackerSyncTickWorkflow>();
            var report = await workflow.RunAsync(stoppingToken);
            LogTick(logger, report.BoardsPolled, report.BoardsFailed, report.BoardsSkipped, report.CardsChanged);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogTickFailed(logger, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "TaskTrackerSyncService disabled: poll_interval_seconds <= 0.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "TaskTrackerSyncService tick: polled={Polled}, failed={Failed}, skipped={Skipped}, cards_changed={CardsChanged}")]
    private static partial void LogTick(ILogger logger, int polled, int failed, int skipped, int cardsChanged);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "TaskTrackerSyncService tick failed; worker continues.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
