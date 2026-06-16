using FluentAssertions;
using NSubstitute;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Terminals;

public class TerminalHookStatusHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    [Theory(DisplayName = "Stop и Notification в work/free/interview паркуют интент в awaiting_operator")]
    [InlineData(TerminalHookEvents.Stop, TerminalRunModes.Work)]
    [InlineData(TerminalHookEvents.Stop, TerminalRunModes.Free)]
    [InlineData(TerminalHookEvents.Stop, TerminalRunModes.Interview)]
    [InlineData(TerminalHookEvents.Notification, TerminalRunModes.Work)]
    [InlineData(TerminalHookEvents.Notification, TerminalRunModes.Free)]
    [InlineData(TerminalHookEvents.Notification, TerminalRunModes.Interview)]
    public async Task Park_events_set_awaiting_operator(string hookEvent, string mode)
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.Work);

        await handler.HandleAsync("intent-1", hookEvent, mode, CancellationToken.None);

        await ReceivedStatusSet(repo, IntentStatusNames.AwaitingOperator);
    }

    [Theory(DisplayName = "UserPromptSubmit и PostToolUse возвращают интент в исходную фазу спавна")]
    [InlineData(TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Work, IntentStatusNames.Work)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Free, IntentStatusNames.Work)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Interview, IntentStatusNames.Interview)]
    [InlineData(TerminalHookEvents.PostToolUse, TerminalRunModes.Work, IntentStatusNames.Work)]
    [InlineData(TerminalHookEvents.PostToolUse, TerminalRunModes.Free, IntentStatusNames.Work)]
    [InlineData(TerminalHookEvents.PostToolUse, TerminalRunModes.Interview, IntentStatusNames.Interview)]
    public async Task Resume_events_return_to_spawn_phase(string hookEvent, string mode, string expected)
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.AwaitingOperator);

        await handler.HandleAsync("intent-1", hookEvent, mode, CancellationToken.None);

        await ReceivedStatusSet(repo, expected);
    }

    [Theory(DisplayName = "Bundle-less dream и пустой mode статус не трогают")]
    [InlineData(TerminalHookEvents.Stop, TerminalRunModes.Dream)]
    [InlineData(TerminalHookEvents.Notification, TerminalRunModes.Dream)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Dream)]
    [InlineData(TerminalHookEvents.PostToolUse, TerminalRunModes.Dream)]
    [InlineData(TerminalHookEvents.Stop, null)]
    [InlineData(TerminalHookEvents.Notification, null)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, null)]
    [InlineData(TerminalHookEvents.PostToolUse, null)]
    public async Task No_status_change_for_unphased(string hookEvent, string? mode)
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.Work);

        await handler.HandleAsync("intent-1", hookEvent, mode, CancellationToken.None);

        await ReceivedNoStatusSet(repo);
    }

    [Fact(DisplayName = "Терминальный статус (done) хук не воскрешает")]
    public async Task Terminal_status_is_not_resurrected()
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.Done);

        await handler.HandleAsync("intent-1", TerminalHookEvents.Stop, TerminalRunModes.Work, CancellationToken.None);

        await ReceivedNoStatusSet(repo);
    }

    [Fact(DisplayName = "Повторный Stop в уже-awaiting_operator делегирует перевод (идемпотентность — в домене)")]
    public async Task Repeated_stop_is_idempotent()
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.AwaitingOperator);

        await handler.HandleAsync("intent-1", TerminalHookEvents.Stop, TerminalRunModes.Work, CancellationToken.None);

        await ReceivedStatusSet(repo, IntentStatusNames.AwaitingOperator);
    }

    [Fact(DisplayName = "Перевод приписывается источнику hook:terminal:<event> от System")]
    public async Task Sets_status_with_hook_source_and_system_author()
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.Work);

        await handler.HandleAsync("intent-1", TerminalHookEvents.Stop, TerminalRunModes.Work, CancellationToken.None);

        await repo.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.AwaitingOperator,
            appendText: null,
            reason: null,
            IntentTrainingAuthor.System,
            "hook:terminal:Stop",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "OpenCode lifecycle smoke: idle/permission паркуют, prompt/tool resume возвращают в work")]
    public async Task Opencode_lifecycle_sequence_parks_and_resumes()
    {
        var state = IntentStatusNames.Work;
        var handler = NewStatefulHandler(() => state, next => state = next);

        await handler.HandleAsync("intent-1", TerminalHookEvents.Stop, TerminalRunModes.Work, CancellationToken.None);
        state.Should().Be(IntentStatusNames.AwaitingOperator);

        await handler.HandleAsync(
            "intent-1", TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Work, CancellationToken.None);
        state.Should().Be(IntentStatusNames.Work);

        await handler.HandleAsync(
            "intent-1", TerminalHookEvents.Notification, TerminalRunModes.Work, CancellationToken.None);
        state.Should().Be(IntentStatusNames.AwaitingOperator);

        await handler.HandleAsync("intent-1", TerminalHookEvents.PostToolUse, TerminalRunModes.Work, CancellationToken.None);
        state.Should().Be(IntentStatusNames.Work);
    }

    private static Task ReceivedStatusSet(IIntentRepository repo, string status) =>
        repo.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(), status, Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

    private static Task ReceivedNoStatusSet(IIntentRepository repo) =>
        repo.DidNotReceive().SetStatusAsync(
            Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

    private static (IIntentRepository Repo, TerminalHookStatusHandler Handler) NewHandler(string currentStatus)
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(ci => Intent.Restore(ci.ArgAt<IntentId>(0), "x", currentStatus, 1, [], Now, Now));
        repo.SetStatusAsync(
                Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci => new SetIntentStatusOutcome.Updated(
                Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], Now, Now)));

        var setStatus = new SetIntentStatusHandler(repo, new PassthroughUnitOfWork(), new FixedClock(Now));
        return (repo, new TerminalHookStatusHandler(repo, setStatus));
    }

    private static TerminalHookStatusHandler NewStatefulHandler(Func<string> getStatus, Action<string> setStatus)
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
            .Returns(ci => Intent.Restore(ci.ArgAt<IntentId>(0), "x", getStatus(), 1, [], Now, Now));
        repo.SetStatusAsync(
                Arg.Any<IntentId>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                setStatus(ci.ArgAt<string>(1));
                return new SetIntentStatusOutcome.Updated(
                    Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], Now, Now));
            });

        var status = new SetIntentStatusHandler(repo, new PassthroughUnitOfWork(), new FixedClock(Now));
        return new TerminalHookStatusHandler(repo, status);
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
