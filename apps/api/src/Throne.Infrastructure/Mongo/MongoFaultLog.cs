using Microsoft.Extensions.Logging;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Дедуп логирования Mongo-связных падений тиков воркера. Первый таймаут в окне 60 с
/// идёт WARN коротким сообщением; следующие в окне — глушатся; после трёх подряд
/// провалов один FAIL со стеком. После успешного тика счётчик и окно сбрасываются.
/// Прочие исключения сразу логируются FAIL — это не транзиентная связность, а bug
/// в тике. Объект stateful, по одному на воркер, потокобезопасным не делается:
/// тики идут последовательно из единичного hosted-service loop.
/// </summary>
internal sealed partial class MongoFaultLog(ILogger logger, TimeProvider clock, string workerName)
{
    private const int EscalateAfter = 3;
    private static readonly TimeSpan WarnWindow = TimeSpan.FromSeconds(60);

    private int _consecutiveFailures;
    private DateTimeOffset _warnSuppressedUntil;

    [LoggerMessage(EventId = 1, Level = LogLevel.Error,
        Message = "{Worker} tick failed.")]
    private static partial void LogTickFailed(ILogger logger, Exception ex, string worker);

    [LoggerMessage(EventId = 2, Level = LogLevel.Error,
        Message = "{Worker}: Mongo недоступна {Count} тиков подряд, эскалация.")]
    private static partial void LogEscalated(ILogger logger, Exception ex, string worker, int count);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "{Worker}: Mongo тик упал по таймауту/соединению ({Detail}). Повтор через backoff.")]
    private static partial void LogTransient(ILogger logger, string worker, string detail);

    public void RecordSuccess()
    {
        _consecutiveFailures = 0;
        _warnSuppressedUntil = default;
    }

    public void RecordFailure(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (!MongoResilience.IsMongoTransient(ex))
        {
            LogTickFailed(logger, ex, workerName);
            _consecutiveFailures = 0;
            _warnSuppressedUntil = default;
            return;
        }

        _consecutiveFailures++;
        if (_consecutiveFailures >= EscalateAfter)
        {
            LogEscalated(logger, ex, workerName, _consecutiveFailures);
            _warnSuppressedUntil = default;
            return;
        }

        var now = clock.GetUtcNow();
        if (now < _warnSuppressedUntil)
        {
            return;
        }

        LogTransient(logger, workerName, Summarize(ex));
        _warnSuppressedUntil = now + WarnWindow;
    }

    private static string Summarize(Exception ex) =>
        $"{ex.GetType().Name}: {ex.Message.Split('\n', 2)[0]}";
}
