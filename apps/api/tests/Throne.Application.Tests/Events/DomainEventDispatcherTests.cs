using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;

namespace Throne.Application.Tests.Events;

/// <summary>
/// Per-handler diagnostic trail + preserved «one bad handler aborts the chain»
/// semantics for the in-process dispatcher.
/// </summary>
public class DomainEventDispatcherTests
{
    [Fact(DisplayName = "Хендлеры зовутся последовательно в порядке регистрации")]
    public async Task Invokes_handlers_sequentially()
    {
        var order = new List<string>();
        var first = HandlerThat(_ => order.Add("first"));
        var second = HandlerThat(_ => order.Add("second"));
        var sut = NewDispatcher(first, second);

        await sut.DispatchAsync(new DummyEvent(), CancellationToken.None);

        order.Should().Equal("first", "second");
    }

    [Fact(DisplayName = "Исключение в хендлере пробрасывается и обрывает цепочку")]
    public async Task Handler_exception_rethrows_and_stops_chain()
    {
        var second = Substitute.For<IDomainEventHandler>();
        var first = HandlerThat(_ => throw new InvalidOperationException("boom"));
        var sut = NewDispatcher(first, second);

        var act = async () => await sut.DispatchAsync(new DummyEvent(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        await second.DidNotReceive().HandleAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Исключение пишется Warning с именем хендлера и события")]
    public async Task Handler_exception_logs_warning_with_context()
    {
        var logger = new RecordingLogger();
        var first = HandlerThat(_ => throw new InvalidOperationException("boom"));
        var sut = new DomainEventDispatcher(new[] { first }, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.DispatchAsync(new DummyEvent(), CancellationToken.None));

        logger.Records.Should().ContainSingle(r =>
            r.Level == LogLevel.Warning
            && r.Message.Contains(nameof(DummyEvent), StringComparison.Ordinal)
            && r.Message.Contains(first.GetType().Name, StringComparison.Ordinal));
    }

    private static DomainEventDispatcher NewDispatcher(params IDomainEventHandler[] handlers) =>
        new(handlers, NullLogger<DomainEventDispatcher>.Instance);

    private static IDomainEventHandler HandlerThat(Action<IDomainEvent> body)
    {
        var handler = Substitute.For<IDomainEventHandler>();
        handler.HandleAsync(Arg.Any<IDomainEvent>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                body((IDomainEvent)call[0]!);
                return Task.CompletedTask;
            });
        return handler;
    }

    private sealed record DummyEvent : IDomainEvent;

    private sealed class RecordingLogger : ILogger<DomainEventDispatcher>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Records.Add((logLevel, formatter(state, exception)));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
