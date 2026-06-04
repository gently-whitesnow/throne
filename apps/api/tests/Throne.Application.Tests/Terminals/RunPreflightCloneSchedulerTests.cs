using FluentAssertions;
using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Terminals;

public class RunPreflightCloneSchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly IntentId IntentId = new("intent-run-queue");

    [Fact(DisplayName = "Pre-flight ставит pending и failed binding'и в clone-очередь")]
    public async Task Enqueues_pending_and_failed_bindings()
    {
        var pending = NewBinding(CloneStatusNames.Pending, "pending");
        var failed = NewBinding(CloneStatusNames.Failed, "failed");
        var ready = NewBinding(CloneStatusNames.Ready, "ready");
        var bindings = Substitute.For<IIntentRepositoryBindingRepository>();
        bindings.FindByIntentAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([pending, failed, ready]));
        bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<SaveBindingOutcome>(
                new SaveBindingOutcome.Saved((IntentRepositoryBinding)call[0]!)));
        var queue = Substitute.For<IRepositoryCloneRequests>();
        var writer = new RepositoryCloneTransitionWriter(bindings, new PassthroughUnitOfWork(), new FixedClock());
        var sut = new RunPreflightCloneScheduler(bindings, queue, writer);

        await sut.EnqueuePendingAndFailedAsync(IntentId, CancellationToken.None);

        failed.State.CloneStatus.Should().Be(CloneStatusNames.Pending);
        await queue.Received(1).EnqueueAsync(pending.Id, Arg.Any<CancellationToken>());
        await queue.Received(1).EnqueueAsync(failed.Id, Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueAsync(ready.Id, Arg.Any<CancellationToken>());
    }

    private static IntentRepositoryBinding NewBinding(string cloneStatus, string repo)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: IntentId,
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", repo),
            WorkspacePath: $"/tmp/throne/{repo}",
            DefaultBranch: "main",
            CloneStatus: cloneStatus,
            CloneError: cloneStatus == CloneStatusNames.Failed ? "previous error" : null,
            PullRequestNumber: null,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBinding.Restore(snapshot);
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
