using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Intents;

public class SetIntentStatusHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "set_intent_status принимает любой известный статус без reason")]
    public async Task Accepts_known_status_without_reason()
    {
        var (repo, handler) = NewHandler();

        var result = await handler.HandleAsync(
            new SetIntentStatusCommand("intent-1", IntentStatusNames.ReadyForWork, Reason: null, IntentTrainingAuthor.Agent, "test"),
            CancellationToken.None);

        result.State.Status.Should().Be(IntentStatusNames.ReadyForWork);
        await repo.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.ReadyForWork,
            appendText: null,
            reason: null,
            IntentTrainingAuthor.Agent,
            "test",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "set_intent_status сохраняет произвольный reason для не-reject перехода")]
    public async Task Accepts_optional_reason_for_non_reject_transition()
    {
        var (repo, handler) = NewHandler();

        await handler.HandleAsync(
            new SetIntentStatusCommand("intent-1", IntentStatusNames.NeedsHelp, Reason: "нужен доступ к prod", IntentTrainingAuthor.Agent, "test"),
            CancellationToken.None);

        await repo.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.NeedsHelp,
            appendText: null,
            reason: "нужен доступ к prod",
            IntentTrainingAuthor.Agent,
            "test",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "set_intent_status reject без reason падает с validation_failed")]
    public async Task Reject_without_reason_fails()
    {
        var (_, handler) = NewHandler();

        var act = () => handler.HandleAsync(
            new SetIntentStatusCommand("intent-1", IntentStatusNames.Reject, Reason: null, IntentTrainingAuthor.User, "test"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
        ex.Which.Extensions["field"].Should().Be("reason");
    }

    [Fact(DisplayName = "set_intent_status reject апендит reason в Intent.text")]
    public async Task Reject_appends_reason_to_text()
    {
        var (repo, handler) = NewHandler();

        await handler.HandleAsync(
            new SetIntentStatusCommand("intent-1", IntentStatusNames.Reject, Reason: "дубль ICE-42", IntentTrainingAuthor.User, "test"),
            CancellationToken.None);

        await repo.Received(1).SetStatusAsync(
            Arg.Any<IntentId>(),
            IntentStatusNames.Reject,
            Arg.Is<string>(s => s != null && s.Contains("Причина отклонения") && s.Contains("дубль ICE-42")),
            "дубль ICE-42",
            IntentTrainingAuthor.User,
            "test",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "set_intent_status отвергает неизвестный статус")]
    public async Task Unknown_status_fails()
    {
        var (_, handler) = NewHandler();

        var act = () => handler.HandleAsync(
            new SetIntentStatusCommand("intent-1", "totally-unknown", Reason: null, IntentTrainingAuthor.Agent, "test"),
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static (IIntentRepository Repo, SetIntentStatusHandler Handler) NewHandler()
    {
        var repo = Substitute.For<IIntentRepository>();
        repo.SetStatusAsync(
                Arg.Any<IntentId>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<IntentTrainingAuthor>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var intentId = ci.ArgAt<IntentId>(0);
                var status = ci.ArgAt<string>(1);
                return new SetIntentStatusOutcome.Updated(
                    Intent.Restore(intentId, "x", status, 1, [], Now, Now));
            });

        var handler = new SetIntentStatusHandler(repo, new PassthroughUnitOfWork(), new FixedClock(Now));
        return (repo, handler);
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
