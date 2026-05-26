using FluentAssertions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class RepositoryCloneWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 24, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";

    [Fact(DisplayName = "RunAsync: успешный клон даёт pending→cloning→ready и два progress-события")]
    public async Task RunAsync_success_progresses_pending_cloning_ready()
    {
        var fixture = new WorkflowFixture();
        var binding = fixture.SeedPendingBinding();

        var result = await fixture.Workflow.RunAsync(binding.Id, CancellationToken.None);

        result.Should().Be(CloneRunResult.Ready);
        // Captured statuses are snapshotted at save time; Events list references the same
        // mutating aggregate (the dispatcher would normally serialise to wire-format
        // immediately, so the binding identity inside the event is what matters here).
        fixture.Captured.Statuses.Should().Equal(CloneStatusNames.Cloning, CloneStatusNames.Ready);
        fixture.Events.OfType<IntentRepositoryCloneProgress>().Should().HaveCount(2);
        fixture.Events.OfType<IntentRepositoryCloneProgress>()
            .Select(e => e.Binding.Id).Should().AllBeEquivalentTo(binding.Id);
        await fixture.Provider.Received(1).CloneRepositoryAsync(
            binding.Coordinate.Owner, binding.Coordinate.Repo, binding.WorkspacePath, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "RunAsync: исключение провайдера переводит binding в failed и эмитит cloning+failed")]
    public async Task RunAsync_provider_failure_marks_failed()
    {
        var fixture = new WorkflowFixture();
        var binding = fixture.SeedPendingBinding();
        fixture.Provider
            .CloneRepositoryAsync(binding.Coordinate.Owner, binding.Coordinate.Repo, binding.WorkspacePath, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => Task.FromException(new InvalidOperationException("gh exit 128: not found")));

        var result = await fixture.Workflow.RunAsync(binding.Id, CancellationToken.None);

        result.Should().Be(CloneRunResult.Failed);
        fixture.Captured.Statuses.Should().Equal(CloneStatusNames.Cloning, CloneStatusNames.Failed);
        fixture.Events.OfType<IntentRepositoryCloneProgress>().Should().HaveCount(2);
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
        binding.State.CloneError.Should().Be("gh exit 128: not found");
    }

    [Fact(DisplayName = "RunAsync: уже не pending — no-op без save")]
    public async Task RunAsync_non_pending_is_noop()
    {
        var fixture = new WorkflowFixture();
        var binding = fixture.SeedBinding(cloneStatus: CloneStatusNames.Ready);

        var result = await fixture.Workflow.RunAsync(binding.Id, CancellationToken.None);

        result.Should().Be(CloneRunResult.AlreadyProcessed);
        await fixture.Bindings.DidNotReceive().SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
        fixture.Events.Should().BeEmpty();
    }

    [Fact(DisplayName = "RunAsync: binding исчез между enqueue и dequeue — NotFound без save")]
    public async Task RunAsync_missing_binding_returns_not_found()
    {
        var fixture = new WorkflowFixture();
        fixture.Bindings.GetByIdAsync(Arg.Any<BindingId>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntentRepositoryBinding?>(null));

        var result = await fixture.Workflow.RunAsync(BindingId.New(), CancellationToken.None);

        result.Should().Be(CloneRunResult.NotFound);
        await fixture.Bindings.DidNotReceive().SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>());
        fixture.Events.Should().BeEmpty();
    }

    [Fact(DisplayName = "RunAsync: незарегистрированный провайдер — ProviderMissing + failed-событие")]
    public async Task RunAsync_unknown_provider_marks_failed()
    {
        var fixture = new WorkflowFixture();
        var binding = fixture.SeedPendingBinding();
        fixture.Providers.GetByName(GitProviderNames.GitHub).Returns((IGitProvider?)null);

        var result = await fixture.Workflow.RunAsync(binding.Id, CancellationToken.None);

        result.Should().Be(CloneRunResult.ProviderMissing);
        fixture.Captured.Statuses.Should().Equal(CloneStatusNames.Failed);
        binding.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
        fixture.Events.OfType<IntentRepositoryCloneProgress>().Should().ContainSingle();
    }

    [Fact(DisplayName = "MarkInterruptedAsync переводит cloning→failed(reason) и эмитит progress-событие")]
    public async Task MarkInterrupted_flips_cloning_to_failed()
    {
        var fixture = new WorkflowFixture();
        var binding = fixture.SeedBinding(cloneStatus: CloneStatusNames.Cloning);

        await fixture.Workflow.MarkInterruptedAsync(binding, "interrupted", CancellationToken.None);

        binding.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
        binding.State.CloneError.Should().Be("interrupted");
        fixture.Events.OfType<IntentRepositoryCloneProgress>().Should().ContainSingle()
            .Which.Binding.State.CloneStatus.Should().Be(CloneStatusNames.Failed);
    }

    private sealed class WorkflowFixture
    {
        public WorkflowFixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Providers = Substitute.For<IGitProviderRegistry>();
            Provider = Substitute.For<IGitProvider>();
            Provider.ProviderName.Returns(GitProviderNames.GitHub);
            Providers.GetByName(GitProviderNames.GitHub).Returns(Provider);

            Captured = new CapturedTransitions();
            Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    var b = ci.Arg<IntentRepositoryBinding>();
                    Captured.Statuses.Add(b.State.CloneStatus);
                    return Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(b));
                });

            var uow = new RecordingUnitOfWork();
            Events = uow.Events;
            var clock = new FixedClock(Now);
            var writer = new RepositoryCloneTransitionWriter(Bindings, uow, clock);
            Workflow = new RepositoryCloneWorkflow(Bindings, Providers, writer);
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public IGitProviderRegistry Providers { get; }
        public IGitProvider Provider { get; }
        public RepositoryCloneWorkflow Workflow { get; }
        public CapturedTransitions Captured { get; }
        public List<IDomainEvent> Events { get; }

        public IntentRepositoryBinding SeedPendingBinding(string intentId = IntentIdValue) =>
            SeedBinding(intentId, CloneStatusNames.Pending);

        public IntentRepositoryBinding SeedBinding(
            string intentId = IntentIdValue,
            string cloneStatus = CloneStatusNames.Pending)
        {
            var binding = NewBinding(intentId, cloneStatus);
            Bindings.GetByIdAsync(binding.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IntentRepositoryBinding?>(binding));
            return binding;
        }
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

    private sealed class CapturedTransitions
    {
        public List<string> Statuses { get; } = [];
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
