using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Repositories;

namespace Throne.Infrastructure.Git;

/// <summary>
/// <see cref="BackgroundService"/> implementing the consumer side of the
/// clone-on-bind queue (ADR-0024 § 5, расширено ADR-0026 §5 параллельностью).
///
/// Boot-up sequence:
/// <list type="number">
///   <item>Recovery pass via <see cref="RepositoryCloneRecoveryWorkflow"/>:
///         <c>cloning → failed("interrupted")</c> + re-queue stranded <c>pending</c>.</item>
///   <item>Drain the in-process queue indefinitely через
///         <see cref="CloneRunnerLoop"/>. Лимит параллелизма — слот в
///         <see cref="SemaphoreSlim"/> с значением
///         <see cref="CloneRunnerOptions.MaxParallel"/>; лишние bindings ждут
///         освобождения слота, сам цикл чтения канала не блокируется.</item>
/// </list>
///
/// Сам worker остаётся тонким: не грузит binding'и, не дёргает <c>git</c>,
/// не трогает диспетчер событий. Вся тяжесть — в Application workflow,
/// который резолвится через свой <see cref="IServiceScope"/> на binding.
/// </summary>
internal sealed partial class RepositoryCloneService(
    IServiceScopeFactory scopeFactory,
    IRepositoryCloneRequestsReader queue,
    IOptions<CloneRunnerOptions> options,
    ILogger<RepositoryCloneService> logger) : BackgroundService
{
    private readonly int _maxParallel = NormalizeMaxParallel(options.Value.MaxParallel);

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "RepositoryCloneService recovery: interrupted={Interrupted}, requeued={Requeued}")]
    private static partial void LogRecovery(ILogger logger, int interrupted, int requeued);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "RepositoryCloneService recovery pass failed; worker continues without it.")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "RepositoryCloneService runner started: max_parallel={MaxParallel}")]
    private static partial void LogRunnerStarted(ILogger logger, int maxParallel);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogRunnerStarted(logger, _maxParallel);
        await RunRecoveryAsync(stoppingToken);
        var loop = new CloneRunnerLoop(scopeFactory, queue, _maxParallel, logger);
        await loop.DrainAsync(stoppingToken);
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

    private static int NormalizeMaxParallel(int configured) => configured <= 0 ? 1 : configured;
}
