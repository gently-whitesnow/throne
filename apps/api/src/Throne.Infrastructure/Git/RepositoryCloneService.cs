using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git;

/// <summary>
/// <see cref="BackgroundService"/> implementing the consumer side of the
/// clone-on-bind queue (ADR-0024 § 5).
///
/// Boot-up sequence:
/// <list type="number">
///   <item>Recovery pass via <see cref="RepositoryCloneRecoveryWorkflow"/>:
///         <c>cloning → failed("interrupted")</c> + re-queue stranded <c>pending</c>.</item>
///   <item>Drain the in-process queue indefinitely; per binding delegate to
///         <see cref="RepositoryCloneWorkflow.RunAsync"/> which owns the
///         state machine and the domain-event emission.</item>
/// </list>
///
/// The worker itself stays thin: it does not load bindings, does not call
/// <c>git</c>, does not touch the event dispatcher. All of that is in the
/// Application workflow so the heavy lifting can be unit-tested without a
/// hosted-service rig. Both workflows are resolved per-iteration through an
/// <see cref="IServiceScopeFactory"/> so any scoped/transient port dependencies
/// (added in later slices) work without surprise re-injection of singletons.
/// </summary>
internal sealed partial class RepositoryCloneService(
    IServiceScopeFactory scopeFactory,
    IRepositoryCloneRequestsReader queue,
    ILogger<RepositoryCloneService> logger) : BackgroundService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "RepositoryCloneService recovery: interrupted={Interrupted}, requeued={Requeued}")]
    private static partial void LogRecovery(ILogger logger, int interrupted, int requeued);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "RepositoryCloneService recovery pass failed; worker continues without it.")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "RepositoryCloneService processed binding {BindingId}: {Result}")]
    private static partial void LogProcessed(ILogger logger, string bindingId, CloneRunResult result);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "RepositoryCloneService failed to process binding {BindingId}; will not retry.")]
    private static partial void LogProcessingFailed(ILogger logger, string bindingId, Exception exception);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunRecoveryAsync(stoppingToken);
        await DrainQueueAsync(stoppingToken);
    }

    private async Task RunRecoveryAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var recovery = scope.ServiceProvider.GetRequiredService<RepositoryCloneRecoveryWorkflow>();
            var report = await recovery.RunAsync(stoppingToken);
            LogRecovery(logger, report.Interrupted, report.Requeued);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown before the worker even started consuming
        }
        catch (Exception ex)
        {
            LogRecoveryFailed(logger, ex);
        }
    }

    private async Task DrainQueueAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var bindingId in queue.ReadAllAsync(stoppingToken))
            {
                await ProcessOneAsync(bindingId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
    }

    private async Task ProcessOneAsync(BindingId bindingId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var workflow = scope.ServiceProvider.GetRequiredService<RepositoryCloneWorkflow>();
            var result = await workflow.RunAsync(bindingId, stoppingToken);
            LogProcessed(logger, bindingId.Value, result);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown — RepositoryCloneWorkflow leaves the binding in
            // cloning so the next process's recovery pass flips it to failed.
            throw;
        }
        catch (Exception ex)
        {
            LogProcessingFailed(logger, bindingId.Value, ex);
        }
    }
}
