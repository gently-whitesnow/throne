using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Imaging;

internal sealed partial class IntentAttachmentCompressionWorker(
    IIntentAttachmentRepository attachments,
    IImageDownscaler downscaler,
    IOptions<IntentAttachmentCompressionOptions> options,
    ResiliencePipeline mongoResilience,
    ILogger<IntentAttachmentCompressionWorker> log) : BackgroundService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "IntentAttachmentCompressionWorker disabled: poll_interval <= 0.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "IntentAttachmentCompressionWorker tick failed.")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "IntentAttachmentCompressionWorker: pending attachment {AttachmentId} has no GridFS blob, skipping.")]
    private static partial void LogMissingBlob(ILogger logger, string attachmentId);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "IntentAttachmentCompressionWorker: failed to compress attachment {AttachmentId}; will retry next tick.")]
    private static partial void LogCompressFailed(ILogger logger, string attachmentId, Exception exception);

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
                    await CompressBatchAsync(attachments, downscaler, settings, log, mongoResilience, stoppingToken);
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

    public static async Task CompressBatchAsync(
        IIntentAttachmentRepository repo,
        IImageDownscaler downscaler,
        IntentAttachmentCompressionOptions settings,
        ILogger? log,
        CancellationToken ct) =>
        await CompressBatchAsync(repo, downscaler, settings, log, mongoResilience: null, ct);

    private static async Task CompressBatchAsync(
        IIntentAttachmentRepository repo,
        IImageDownscaler downscaler,
        IntentAttachmentCompressionOptions settings,
        ILogger? log,
        ResiliencePipeline? mongoResilience,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(downscaler);
        ArgumentNullException.ThrowIfNull(settings);

        var logger = log ?? NullLogger.Instance;

        var batch = await ExecuteMongoAsync(
            mongoResilience,
            inner => repo.ListPendingCompressionAsync(settings.BatchSize, inner),
            ct);
        if (batch.Count == 0)
        {
            return;
        }

        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            await CompressOneAsync(repo, downscaler, settings, item, logger, mongoResilience, ct);
        }
    }

    private static async Task CompressOneAsync(
        IIntentAttachmentRepository repo,
        IImageDownscaler downscaler,
        IntentAttachmentCompressionOptions settings,
        PendingCompressionItem item,
        ILogger logger,
        ResiliencePipeline? mongoResilience,
        CancellationToken ct)
    {
        try
        {
            await using var raw = await ExecuteMongoAsync(
                mongoResilience,
                inner => repo.OpenRawContentAsync(item.GridFsId, inner),
                ct);
            if (raw is null)
            {
                LogMissingBlob(logger, item.AttachmentId);
                return;
            }

            var compressed = await downscaler.DownscaleAsync(raw, item.ContentType, settings.MaxDimension, settings.JpegQuality, ct);
            await ExecuteMongoAsync(
                mongoResilience,
                inner => repo.ApplyCompressionAsync(item.AttachmentId, item.GridFsId, compressed, inner),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogCompressFailed(logger, item.AttachmentId, ex);
        }
    }

    private static Task<T> ExecuteMongoAsync<T>(
        ResiliencePipeline? mongoResilience,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct) =>
        mongoResilience is null
            ? operation(ct)
            : MongoResilience.ExecuteAsync(mongoResilience, operation, ct);

    private static Task ExecuteMongoAsync(
        ResiliencePipeline? mongoResilience,
        Func<CancellationToken, Task> operation,
        CancellationToken ct) =>
        mongoResilience is null
            ? operation(ct)
            : MongoResilience.ExecuteAsync(mongoResilience, operation, ct);
}
