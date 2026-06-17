using FluentAssertions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Throne.Infrastructure.Mongo;

namespace Throne.Infrastructure.Tests.Mongo;

public class MongoFaultLogTests
{
    [Fact(DisplayName = "Первый таймаут идёт WARN, второй в окне 60с глушится")]
    public void Warn_then_suppress_in_window()
    {
        var sink = new ListLogger();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var log = new MongoFaultLog(sink, clock, "TestWorker");

        log.RecordFailure(NewMongoTimeout());
        clock.Advance(TimeSpan.FromSeconds(30));
        log.RecordFailure(NewMongoTimeout());

        sink.Entries.Should().ContainSingle().Which.Level.Should().Be(LogLevel.Warning);
    }

    [Fact(DisplayName = "После выхода из 60с окна WARN снова допускается")]
    public void Warn_again_after_window_expires()
    {
        var sink = new ListLogger();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var log = new MongoFaultLog(sink, clock, "TestWorker");

        log.RecordFailure(NewMongoTimeout());
        clock.Advance(TimeSpan.FromSeconds(61));
        log.RecordFailure(NewMongoTimeout());

        sink.Entries.Should().HaveCount(2);
        sink.Entries.Should().OnlyContain(e => e.Level == LogLevel.Warning);
    }

    [Fact(DisplayName = "Третий подряд Mongo-сбой эскалируется в Error со стеком")]
    public void Third_consecutive_failure_escalates_to_error()
    {
        var sink = new ListLogger();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var log = new MongoFaultLog(sink, clock, "TestWorker");

        log.RecordFailure(NewMongoTimeout());
        log.RecordFailure(NewMongoTimeout());
        log.RecordFailure(NewMongoTimeout());

        sink.Entries[^1].Level.Should().Be(LogLevel.Error);
        sink.Entries[^1].Exception.Should().NotBeNull("эскалация обязана нести стек");
    }

    [Fact(DisplayName = "RecordSuccess сбрасывает счётчик и окно")]
    public void Success_resets_state()
    {
        var sink = new ListLogger();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var log = new MongoFaultLog(sink, clock, "TestWorker");

        log.RecordFailure(NewMongoTimeout());
        log.RecordFailure(NewMongoTimeout());
        log.RecordSuccess();
        log.RecordFailure(NewMongoTimeout());

        sink.Entries.Should().HaveCount(2);
        sink.Entries.Should().OnlyContain(e => e.Level == LogLevel.Warning);
    }

    [Fact(DisplayName = "Не-Mongo исключение логируется Error сразу со стеком")]
    public void Non_mongo_exception_logs_error_immediately()
    {
        var sink = new ListLogger();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var log = new MongoFaultLog(sink, clock, "TestWorker");

        log.RecordFailure(new InvalidOperationException("boom"));

        sink.Entries.Should().ContainSingle()
            .Which.Should().Match<LogEntry>(e => e.Level == LogLevel.Error && e.Exception is InvalidOperationException);
    }

    private static MongoConnectionException NewMongoTimeout() =>
        new(new MongoDB.Driver.Core.Connections.ConnectionId(
                new MongoDB.Driver.Core.Servers.ServerId(
                    new MongoDB.Driver.Core.Clusters.ClusterId(1),
                    new System.Net.DnsEndPoint("localhost", 27017))),
            "heartbeat timeout",
            new TimeoutException("heartbeat timeout"));

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class ListLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
