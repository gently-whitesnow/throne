using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Events;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Tests.Intents;

public class AddIntentQaHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "00000000000000000000000000000001";

    [Fact(DisplayName = "Appended → Ack с CurrentVersion")]
    public async Task Appended_returns_ack()
    {
        var handler = NewHandler(out var repo);
        repo.AddQaAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(call => new AppendTrainingOutcome.Appended(2, new IntentQaAdded(call.Arg<IntentQa>())));

        var ack = await handler.HandleAsync(
            new AddIntentQaCommand(IntentIdValue, ExpectedVersion: 2, Question: "q?", Answer: "a"),
            CancellationToken.None);

        ack.IntentId.Should().Be(IntentIdValue);
        ack.CurrentVersion.Should().Be(2);
        ack.Accepted.Should().BeTrue();
    }

    [Fact(DisplayName = "NotFound → ApiException(intent.not_found)")]
    public async Task NotFound_throws()
    {
        var handler = NewHandler(out var repo);
        repo.AddQaAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new AppendTrainingOutcome.NotFound());

        var act = () => handler.HandleAsync(
            new AddIntentQaCommand(IntentIdValue, 1, "q", "a"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "VersionConflict → ApiException(intent.version_conflict)")]
    public async Task VersionConflict_throws()
    {
        var handler = NewHandler(out var repo);
        repo.AddQaAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new AppendTrainingOutcome.VersionConflict(7));

        var act = () => handler.HandleAsync(
            new AddIntentQaCommand(IntentIdValue, 3, "q", "a"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentVersionConflict);
        ex.Extensions["expected_version"].Should().Be(3);
        ex.Extensions["current_version"].Should().Be(7);
    }

    [Fact(DisplayName = "Empty question → validation.failed")]
    public async Task Empty_question_fails_validation()
    {
        var handler = NewHandler(out _);

        var act = () => handler.HandleAsync(
            new AddIntentQaCommand(IntentIdValue, 1, "", "a"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static AddIntentQaHandler NewHandler(out IIntentTrainingRepository repo)
    {
        repo = Substitute.For<IIntentTrainingRepository>();
        return new AddIntentQaHandler(repo, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
