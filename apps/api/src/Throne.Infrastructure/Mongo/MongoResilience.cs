using MongoDB.Driver;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Полли-pipeline для фоновых тиков, ходящих в Mongo. Делает per-attempt timeout +
/// экспоненциальный backoff на ретраях: heartbeat-провал → один тик ждёт,
/// а не валит весь воркер на 30 с (см. инцидент 2026-06-17).
/// Вызывающий код считает «consecutive tick failures» сам и эскалирует уровень
/// лога — Polly здесь только за low-level retry/backoff.
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

    // Server selection timeout приходит как System.TimeoutException из Cluster.ThrowTimeoutException
    // (см. инцидент 2026-06-17). MongoConnectionException — heartbeat/socket-провалы. Pool-paused
    // и execution-timeout относим к транзиентным: первое — последствие хвостового handshake, второе —
    // op timeout, retry-safe для read-only тиков воркеров.
    public static bool IsMongoTransient(Exception? ex) => ex is
        MongoConnectionException
        or MongoConnectionPoolPausedException
        or MongoExecutionTimeoutException
        or TimeoutException
        or TimeoutRejectedException;
}
