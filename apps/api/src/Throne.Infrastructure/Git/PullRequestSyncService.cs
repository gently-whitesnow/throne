using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Repositories;

namespace Throne.Infrastructure.Git;

/// <summary>
/// <see cref="BackgroundService"/> implementing the polling side of the PR review-comment
/// sync (ADR-0024 § 6). Thin host wrapper: the per-tick orchestration lives in
/// the Application-layer <see cref="PullRequestSyncTickWorkflow"/> so it can be
/// unit-tested without a hosted-service rig.
///
/// Tick cadence is driven by <see cref="PullRequestSyncOptions.PollIntervalSeconds"/>;
/// per-binding rate-limit / failure backoff is owned by <see cref="PullRequestSyncBackoff"/>.
/// Tick failures are caught and logged so a single bad iteration does not stop the
/// hosted service.
/// </summary>
internal sealed partial class PullRequestSyncService(
    IServiceScopeFactory scopeFactory,
    IOptions<PullRequestSyncOptions> options,
    ILogger<PullRequestSyncService> logger) : BackgroundService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "PullRequestSyncService disabled: poll_interval_seconds <= 0.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "PullRequestSyncService tick: polled={Polled}, not_modified={NotModified}, new_comments={NewComments}, skipped={Skipped}, failed={Failed}, marked_broken={MarkedBroken}, lifecycle_closed={LifecycleClosed}")]
    private static partial void LogTick(
        ILogger logger,
        int polled,
        int notModified,
        int newComments,
        int skipped,
        int failed,
        int markedBroken,
        int lifecycleClosed);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error,
        Message = "PullRequestSyncService tick failed; worker continues.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "PullRequestSyncService auto-bind pass: bound={Bound}, skipped={Skipped}, failed={Failed}")]
    private static partial void LogAutoBind(ILogger logger, int bound, int skipped, int failed);

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

            var autoBind = scope.ServiceProvider.GetRequiredService<PullRequestAutoBindWorkflow>();
            var autoBindReport = await autoBind.RunAsync(stoppingToken);
            LogAutoBind(logger, autoBindReport.Bound, autoBindReport.Skipped, autoBindReport.Failed);

            var workflow = scope.ServiceProvider.GetRequiredService<PullRequestSyncTickWorkflow>();
            var report = await workflow.RunAsync(stoppingToken);
            var snapshot = report.Snapshot;
            LogTick(
                logger,
                snapshot.Polled,
                snapshot.NotModified,
                snapshot.NewComments,
                snapshot.Skipped,
                snapshot.Failed,
                snapshot.MarkedBroken,
                snapshot.LifecycleClosed);
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
}
