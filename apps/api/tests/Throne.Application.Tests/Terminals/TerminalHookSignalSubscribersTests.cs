using FluentAssertions;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TerminalHookSignalSubscribersTests
{
    [Fact(DisplayName = "SessionReady subscriber завершает readiness latch")]
    public async Task Session_ready_signals_readiness_latch()
    {
        var signals = new TerminalReadinessSignals();
        using var readiness = signals.Arm("intent-1");
        var subscriber = new TerminalReadinessSignalSubscriber(signals);

        await subscriber.HandleAsync(
            new TerminalHookEvent("intent-1", TerminalHookEvents.SessionReady, TerminalRunModes.Work, DateTimeOffset.UtcNow),
            CancellationToken.None);

        readiness.Ready.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact(DisplayName = "UserPromptSubmit subscriber завершает submit latch")]
    public async Task User_prompt_submit_signals_submit_latch()
    {
        var signals = new TerminalPromptSubmitSignals();
        using var submit = signals.Arm("intent-1");
        var subscriber = new TerminalPromptSubmitSignalSubscriber(signals);

        await subscriber.HandleAsync(
            new TerminalHookEvent(
                "intent-1",
                TerminalHookEvents.UserPromptSubmit,
                TerminalRunModes.Work,
                DateTimeOffset.UtcNow),
            CancellationToken.None);

        submit.Submitted.IsCompletedSuccessfully.Should().BeTrue();
    }
}
