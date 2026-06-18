using MongoDB.Driver;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Polly-pipeline для Mongo-вызовов из фоновых воркеров. Делает per-attempt timeout
/// + экспоненциальный backoff на ретраях: heartbeat-провал ждёт на границе Mongo-call,
/// не смешиваясь с файловыми, CLI или CPU-bound шагами тика.
/// </summary>
internal static class MongoResilience
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
    private const int MaxRetries = 5;

    public static ResiliencePipeline Build(MongoClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var perAttempt = TimeSpan.FromSeconds(options.ServerSelectionTimeoutSeconds + 5);

        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = static args => IsMongoTransient(args.Outcome.Exception)
                    ? PredicateResult.True()
                    : PredicateResult.False(),
                MaxRetryAttempts = MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = BaseDelay,
                MaxDelay = MaxDelay,
                UseJitter = true,
            })
            .AddTimeout(new TimeoutStrategyOptions { Timeout = perAttempt })
            .Build();
    }

    public static async Task<T> ExecuteAsync<T>(
        ResiliencePipeline pipeline,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(operation);

        return await pipeline.ExecuteAsync(
            async inner => await operation(inner),
            ct);
    }

    public static async Task ExecuteAsync(
        ResiliencePipeline pipeline,
        Func<CancellationToken, Task> operation,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(operation);

        await pipeline.ExecuteAsync(
            async inner => await operation(inner),
            ct);
    }

    // Server selection timeout приходит как System.TimeoutException из Cluster.ThrowTimeoutException
    // (см. инцидент 2026-06-17). Поэтому TimeoutException допустим здесь только при
    // использовании pipeline вокруг конкретного Mongo driver call.
    public static bool IsMongoTransient(Exception? ex) => ex is
        MongoConnectionException
        or MongoConnectionPoolPausedException
        or MongoExecutionTimeoutException
        or TimeoutException
        or TimeoutRejectedException;
}
