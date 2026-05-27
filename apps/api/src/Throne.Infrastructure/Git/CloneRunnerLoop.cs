using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Параллельный consumer-loop клон-очереди (ADR-0026 §5). Лимит параллелизма —
/// <see cref="SemaphoreSlim"/>; cам ProcessOne — отдельный scope на binding.
/// Вынесено из <see cref="RepositoryCloneService"/> чтобы хост-класс остался
/// тонкой обёрткой над recovery + drain, не упирался в CA1502.
/// </summary>
internal sealed partial class CloneRunnerLoop(
    IServiceScopeFactory scopeFactory,
    IRepositoryCloneRequestsReader queue,
    int maxParallel,
    ILogger logger)
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information,
        Message = "RepositoryCloneService processed binding {BindingId}: {Result}")]
    private static partial void LogProcessed(ILogger logger, string bindingId, CloneRunResult result);

    [LoggerMessage(EventId = 101, Level = LogLevel.Warning,
        Message = "RepositoryCloneService failed to process binding {BindingId}; will not retry.")]
    private static partial void LogProcessingFailed(ILogger logger, string bindingId, Exception exception);

    public async Task DrainAsync(CancellationToken stoppingToken)
    {
        using var slots = new SemaphoreSlim(maxParallel, maxParallel);
        var inFlight = new HashSet<Task>();

        try
        {
            await foreach (var bindingId in queue.ReadAllAsync(stoppingToken))
            {
                await slots.WaitAsync(stoppingToken);
                var task = ProcessOneAsync(bindingId, slots, stoppingToken);
                inFlight.Add(task);
                inFlight.RemoveWhere(static t => t.IsCompleted);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown — active clones honour linked-token; recovery
            // pass следующего процесса перевернёт оставшиеся cloning → failed.
        }

        await Task.WhenAll(inFlight);
    }

    private async Task ProcessOneAsync(
        BindingId bindingId,
        SemaphoreSlim slots,
        CancellationToken stoppingToken)
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
            // graceful shutdown — workflow leaves the binding in `cloning`; the
            // next process's recovery pass flips it to failed.
        }
        catch (Exception ex)
        {
            LogProcessingFailed(logger, bindingId.Value, ex);
        }
        finally
        {
            slots.Release();
        }
    }
}
