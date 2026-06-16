using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Api.Terminals;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Tests.Terminals;

public class TerminalHookStatusAckTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "Не-UserPromptSubmit event'ы возвращают тело без hookSpecificOutput")]
    [InlineData(Event.Stop)]
    [InlineData(Event.Notification)]
    [InlineData(Event.PostToolUse)]
    public async Task Non_user_prompt_submit_returns_empty_body(Event @event)
    {
        var (sut, attachments) = NewAck(Array.Empty<IntentAttachment>());

        var response = await sut.HandleAsync("intent-1", @event, TerminalRunMode.Work, CancellationToken.None);

        response.HookSpecificOutput.Should().BeNull();
        await attachments.DidNotReceive().ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "UserPromptSubmit без аттачей не вкладывает hookSpecificOutput")]
    public async Task User_prompt_submit_without_attachments_omits_hook_output()
    {
        var (sut, _) = NewAck(Array.Empty<IntentAttachment>());

        var response = await sut.HandleAsync(
            "intent-1", Event.UserPromptSubmit, TerminalRunMode.Work, CancellationToken.None);

        response.HookSpecificOutput.Should().BeNull();
    }

    [Fact(DisplayName = "UserPromptSubmit с аттачами отдаёт hookSpecificOutput с additionalContext")]
    public async Task User_prompt_submit_with_attachments_emits_additional_context()
    {
        var attachment = new IntentAttachment(
            Id: "att-1",
            IntentId: "intent-1",
            FileName: "shot.png",
            ContentType: "image/png",
            SizeBytes: 100,
            CreatedAt: Now);
        var (sut, _) = NewAck(new[] { attachment });

        var response = await sut.HandleAsync(
            "intent-1", Event.UserPromptSubmit, TerminalRunMode.Work, CancellationToken.None);

        response.HookSpecificOutput.Should().NotBeNull();
        response.HookSpecificOutput!.HookEventName
            .Should().Be(TerminalUserPromptSubmitHookOutputHookEventName.UserPromptSubmit);
        response.HookSpecificOutput.AdditionalContext
            .Should().Be("[intent attachments]\n- id=att-1 kind=image filename=shot.png");
    }

    private static (TerminalHookStatusAck Ack, IIntentAttachmentRepository Attachments) NewAck(
        IReadOnlyList<IntentAttachment> attachments)
    {
        var intents = Substitute.For<IIntentRepository>();
        intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(ci => Intent.Restore(
                ci.ArgAt<IntentId>(0), "x", IntentStatusNames.Work, 1, [], Now, Now));
        intents.SetStatusAsync(
                Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => new SetIntentStatusOutcome.Updated(
                Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], Now, Now)));

        var setStatus = new SetIntentStatusHandler(intents, new PassthroughUnitOfWork(), new FixedClock(Now));
        var hookStatus = new TerminalHookStatusHandler(intents, setStatus);

        var attachmentsRepo = Substitute.For<IIntentAttachmentRepository>();
        attachmentsRepo.ListByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(attachments);
        var contextHandler = new UserPromptSubmitHookContextHandler(attachmentsRepo);

        var ack = new TerminalHookStatusAck(
            hookStatus, contextHandler, NullLogger<TerminalHookStatusAck>.Instance);
        return (ack, attachmentsRepo);
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
