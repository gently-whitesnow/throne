using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Api.Terminals;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Tests.Terminals;

public class TerminalHookStatusAckTests
{
    [Fact(DisplayName = "SessionReady hook завершает readiness latch и не читает intent для статуса")]
    public async Task Session_ready_signals_readiness_without_status_lookup()
    {
        var repository = Substitute.For<IIntentRepository>();
        var status = new SetIntentStatusHandler(
            repository,
            Substitute.For<IUnitOfWork>(),
            TimeProvider.System);
        var signals = new TerminalReadinessSignals();
        var submitSignals = new TerminalPromptSubmitSignals();
        using var readiness = signals.Arm("intent-1");
        var ack = new TerminalHookStatusAck(
            new TerminalHookStatusHandler(repository, status),
            signals,
            submitSignals,
            NullLogger<TerminalHookStatusAck>.Instance);

        await ack.HandleAsync(
            "intent-1",
            Event.SessionReady,
            TerminalRunMode.Work,
            CancellationToken.None);

        readiness.Ready.IsCompletedSuccessfully.Should().BeTrue();
        await repository.DidNotReceiveWithAnyArgs()
            .GetByIdAsync(default!, default);
    }

    [Fact(DisplayName = "UserPromptSubmit hook завершает submit latch")]
    public async Task User_prompt_submit_signals_submit_latch()
    {
        var repository = Substitute.For<IIntentRepository>();
        var status = new SetIntentStatusHandler(
            repository,
            Substitute.For<IUnitOfWork>(),
            TimeProvider.System);
        var signals = new TerminalReadinessSignals();
        var submitSignals = new TerminalPromptSubmitSignals();
        using var submit = submitSignals.Arm("intent-1");
        var ack = new TerminalHookStatusAck(
            new TerminalHookStatusHandler(repository, status),
            signals,
            submitSignals,
            NullLogger<TerminalHookStatusAck>.Instance);

        await ack.HandleAsync(
            "intent-1",
            Event.UserPromptSubmit,
            TerminalRunMode.Work,
            CancellationToken.None);

        submit.Submitted.IsCompletedSuccessfully.Should().BeTrue();
    }
}
