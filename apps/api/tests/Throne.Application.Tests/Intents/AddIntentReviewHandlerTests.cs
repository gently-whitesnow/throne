using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Application.Ports;

namespace Throne.Application.Tests.Intents;

public class AddIntentReviewHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "00000000000000000000000000000001";

    [Fact(DisplayName = "Appended → Ack")]
    public async Task Appended_returns_ack()
    {
        var handler = NewHandler(out var repo);
        repo.AddReviewAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new AppendTrainingOutcome.Appended(5));

        var ack = await handler.HandleAsync(
            new AddIntentReviewCommand(IntentIdValue, ExpectedVersion: 5, Note: "n", Reason: "r"),
            CancellationToken.None);

        ack.CurrentVersion.Should().Be(5);
        ack.Accepted.Should().BeTrue();
    }

    [Fact(DisplayName = "VersionConflict → ApiException(intent.version_conflict)")]
    public async Task VersionConflict_throws()
    {
        var handler = NewHandler(out var repo);
        repo.AddReviewAsync(default, default, default!, default, default)
            .ReturnsForAnyArgs(new AppendTrainingOutcome.VersionConflict(9));

        var act = () => handler.HandleAsync(
            new AddIntentReviewCommand(IntentIdValue, 4, "n", "r"),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.IntentVersionConflict);
        ex.Extensions["current_version"].Should().Be(9);
    }

    [Fact(DisplayName = "Empty reason → validation.failed")]
    public async Task Empty_reason_fails_validation()
    {
        var handler = NewHandler(out _);

        var act = () => handler.HandleAsync(
            new AddIntentReviewCommand(IntentIdValue, 1, "n", ""),
            CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.Code.Should().Be(ErrorCodes.ValidationFailed);
    }

    private static AddIntentReviewHandler NewHandler(out IIntentTrainingRepository repo)
    {
        repo = Substitute.For<IIntentTrainingRepository>();
        return new AddIntentReviewHandler(repo, new PassthroughUnitOfWork(), new FakeTimeProvider(Now));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }
}
