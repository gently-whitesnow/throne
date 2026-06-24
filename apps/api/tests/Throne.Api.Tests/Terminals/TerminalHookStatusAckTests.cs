using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Api.Terminals;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Tests.Terminals;

public class TerminalHookStatusAckTests
{
    [Fact(DisplayName = "Hook ack публикует событие в terminal-hook шину")]
    public async Task Publishes_terminal_hook_event()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero));
        var bus = new RecordingTerminalHookBus();
        var ack = new TerminalHookStatusAck(bus, clock, NullLogger<TerminalHookStatusAck>.Instance);

        await ack.HandleAsync(
            "intent-1",
            Event.UserPromptSubmit,
            TerminalRunMode.Review,
            CancellationToken.None);

        bus.Events.Should().ContainSingle().Which.Should().Be(
            new TerminalHookEvent(
                "intent-1",
                TerminalHookEvents.UserPromptSubmit,
                TerminalRunModes.Review,
                clock.GetUtcNow()));
    }

    private sealed class RecordingTerminalHookBus : ITerminalHookBus
    {
        public List<TerminalHookEvent> Events { get; } = [];

        public ValueTask PublishAsync(TerminalHookEvent hook, CancellationToken ct)
        {
            Events.Add(hook);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
