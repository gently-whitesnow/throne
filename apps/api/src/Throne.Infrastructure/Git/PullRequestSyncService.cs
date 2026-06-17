using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Throne.Application.Git;
using Throne.Application.Repositories;
using Throne.Infrastructure.Mongo;

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
    ResiliencePipeline mongoResilience,
    TimeProvider clock,
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

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "PullRequestSyncService auto-bind pass: bound={Bound}, skipped={Skipped}, failed={Failed}")]
    private static partial void LogAutoBind(ILogger logger, int bound, int skipped, int failed);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "PullRequestSyncService binding failure: binding={BindingId}, kind={Kind}, detail={Detail}")]
    private static partial void LogBindingFailure(
        ILogger logger, string bindingId, GitProviderErrorKind kind, string detail);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug,
        Message = "PullRequestSyncService binding offline (transient network): binding={BindingId}, detail={Detail}")]
    private static partial void LogBindingOffline(ILogger logger, string bindingId, string detail);

    // Bindings currently reporting NetworkError. Used to de-dup the offline log:
    // a transiently-unreachable corp host (ADR-0032 § 7) is an expected state, so
    // we log it once at Debug on transition into "offline" instead of a Warning
    // every tick. Owned by the single-threaded tick loop — no synchronisation.
    private HashSet<string> _networkDownBindingIds = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PollIntervalSeconds));
        if (options.Value.PollIntervalSeconds <= 0)
        {
            LogDisabled(logger);
            return;
        }

        var faultLog = new MongoFaultLog(logger, clock, "PullRequestSyncService");
        using var timer = new PeriodicTimer(interval);
        try
        {
            do
            {
                await RunTickAsync(faultLog, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task RunTickAsync(MongoFaultLog faultLog, CancellationToken stoppingToken)
    {
        try
        {
            await mongoResilience.ExecuteAsync(
                async ct => await TickBodyAsync(ct),
                stoppingToken);
            faultLog.RecordSuccess();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            faultLog.RecordFailure(ex);
        }
    }

    private async Task TickBodyAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var autoBind = scope.ServiceProvider.GetRequiredService<PullRequestAutoBindWorkflow>();
        var autoBindReport = await autoBind.RunAsync(ct);
        LogAutoBind(logger, autoBindReport.Bound, autoBindReport.Skipped, autoBindReport.Failed);

        var workflow = scope.ServiceProvider.GetRequiredService<PullRequestSyncTickWorkflow>();
        var report = await workflow.RunAsync(ct);
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
        var networkDownNow = new HashSet<string>();
        foreach (var failure in report.Failures)
        {
            if (failure.Kind == GitProviderErrorKind.NetworkError)
            {
                // Expected transient state (host off-VPN). Debug + de-dup:
                // log only on transition into offline, not every tick.
                networkDownNow.Add(failure.BindingId);
                if (!_networkDownBindingIds.Contains(failure.BindingId))
                {
                    LogBindingOffline(logger, failure.BindingId, failure.Message);
                }
            }
            else
            {
                // Genuine failures (auth / CLI) stay Warning every tick.
                LogBindingFailure(logger, failure.BindingId, failure.Kind, failure.Message);
            }
        }

        // Drop bindings that recovered or were skipped by backoff this tick,
        // so a later relapse re-logs once instead of staying silent forever.
        _networkDownBindingIds = networkDownNow;
    }
}
