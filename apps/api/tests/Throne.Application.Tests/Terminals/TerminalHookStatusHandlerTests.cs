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

    [Theory(DisplayName = "Stop-хук в work/interview паркует интент в awaiting_operator")]
    [InlineData(TerminalRunModes.Work)]
    [InlineData(TerminalRunModes.Interview)]
    public async Task Stop_parks_in_awaiting_operator(string mode)
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.Work);

        await handler.HandleAsync("intent-1", TerminalHookEvents.Stop, mode, CancellationToken.None);

        await ReceivedStatusSet(repo, IntentStatusNames.AwaitingOperator);
    }

    [Theory(DisplayName = "UserPromptSubmit возвращает интент в исходную фазу спавна")]
    [InlineData(TerminalRunModes.Work, IntentStatusNames.Work)]
    [InlineData(TerminalRunModes.Interview, IntentStatusNames.Interview)]
    public async Task UserPromptSubmit_returns_to_spawn_phase(string mode, string expected)
    {
        var (repo, handler) = NewHandler(currentStatus: IntentStatusNames.AwaitingOperator);

        await handler.HandleAsync("intent-1", TerminalHookEvents.UserPromptSubmit, mode, CancellationToken.None);

        await ReceivedStatusSet(repo, expected);
    }

    [Theory(DisplayName = "Bundle-less режимы (dream/free) и пустой mode статус не трогают")]
    [InlineData(TerminalHookEvents.Stop, TerminalRunModes.Dream)]
    [InlineData(TerminalHookEvents.Stop, TerminalRunModes.Free)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Dream)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, TerminalRunModes.Free)]
    [InlineData(TerminalHookEvents.Stop, null)]
    [InlineData(TerminalHookEvents.UserPromptSubmit, null)]
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
