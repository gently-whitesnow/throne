using FluentAssertions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class RepositoryCloneRecoveryWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "RunAsync: cloning-binding'и → failed('interrupted'), pending — переочередь")]
    public async Task RunAsync_marks_cloning_failed_and_requeues_pending()
    {
        var fixture = new RecoveryFixture();
        var stuck1 = NewBinding("intent-1", CloneStatusNames.Cloning);
        var stuck2 = NewBinding("intent-2", CloneStatusNames.Cloning);
        var pending1 = NewBinding("intent-3", CloneStatusNames.Pending);
        var pending2 = NewBinding("intent-4", CloneStatusNames.Pending);

        fixture.Bindings.FindByCloneStatusAsync(CloneStatusNames.Cloning, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([stuck1, stuck2]));
        fixture.Bindings.FindByCloneStatusAsync(CloneStatusNames.Pending, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([pending1, pending2]));

        var report = await fixture.Recovery.RunAsync(CancellationToken.None);

        report.Interrupted.Should().Be(2);
        report.Requeued.Should().Be(2);
        stuck1.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
        stuck1.State.CloneError.Should().Be("interrupted");
        stuck2.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
        fixture.Queue.Enqueued.Should().Equal(pending1.Id, pending2.Id);
        // each failed binding emits its own progress event
        fixture.Events.OfType<IntentRepositoryCloneProgress>().Should().HaveCount(2);
    }

    [Fact(DisplayName = "RunAsync: пустая БД — отчёт 0/0, ноль эмитов")]
    public async Task RunAsync_empty_database_is_noop()
    {
        var fixture = new RecoveryFixture();
        fixture.Bindings.FindByCloneStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([]));

        var report = await fixture.Recovery.RunAsync(CancellationToken.None);

        report.Interrupted.Should().Be(0);
        report.Requeued.Should().Be(0);
        fixture.Queue.Enqueued.Should().BeEmpty();
        fixture.Events.Should().BeEmpty();
    }

    private static IntentRepositoryBinding NewBinding(string intentId, string cloneStatus)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(intentId),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            WorkspacePath: $"/tmp/throne-test-workspaces/intents/{intentId}/octo__hello",
            DefaultBranch: "main",
            CloneStatus: cloneStatus,
            CloneError: null,
            PullRequestNumber: null,
            PullRequestState: null,
            ReviewCommentsEtag: null,
            LastSeenReviewCommentAt: null,
            LastSyncedAt: null,
            CreatedAt: Now,
            UpdatedAt: Now);
        return IntentRepositoryBinding.Restore(snapshot);
    }

    private sealed class RecoveryFixture
    {
        public RecoveryFixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SaveBindingOutcome>(
                    new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));

            Queue = new RecordingCloneRequests();
            var uow = new RecordingUnitOfWork();
            Events = uow.Events;
            var providers = Substitute.For<IGitProviderRegistry>();
            var writer = new RepositoryCloneTransitionWriter(Bindings, uow, new FixedClock(Now));
            var workflow = new RepositoryCloneWorkflow(Bindings, providers, writer);
            Recovery = new RepositoryCloneRecoveryWorkflow(Bindings, Queue, workflow);
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public RecordingCloneRequests Queue { get; }
        public RepositoryCloneRecoveryWorkflow Recovery { get; }
        public List<IDomainEvent> Events { get; }
    }

    private sealed class RecordingCloneRequests : IRepositoryCloneRequests
    {
        public List<BindingId> Enqueued { get; } = [];

        public ValueTask EnqueueAsync(BindingId bindingId, CancellationToken ct)
        {
            Enqueued.Add(bindingId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        {
            var result = await work(ct);
            if (result is IDomainEventCarrier carrier)
            {
                Events.AddRange(carrier.Events);
            }
            return result;
        }

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            ExecuteAsync(work, ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
