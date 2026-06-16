using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

/// <summary>
/// Auto-bind pass (intent spec A): a ready binding with an empty PR slot gets the open PR whose
/// head matches the clone's current branch attached — but only on an exactly-one match.
/// </summary>
public class PullRequestAutoBindWorkflowTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    private const string Branch = "feat/x";

    [Fact(DisplayName = "Ровно один open-PR с head == текущая ветка → PR прикреплён, Bound++")]
    public async Task Attaches_pull_request_on_single_head_match()
    {
        var fixture = new Fixture();
        var binding = fixture.SeedReadyBinding();
        fixture.SeedBranch(Branch);
        fixture.SeedOpenPrs(new GitPullRequestRef(42, "x", Branch, PullRequestStateNames.Open));

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        report.Bound.Should().Be(1);
        binding.State.PullRequestNumber.Should().Be(42);
        await fixture.Bindings.Received(1).SaveAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.State.PullRequestNumber == 42), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Ноль совпадений head → пропуск, PR не привязан")]
    public async Task Skips_when_no_head_match()
    {
        var fixture = new Fixture();
        var binding = fixture.SeedReadyBinding();
        fixture.SeedBranch(Branch);
        fixture.SeedOpenPrs(new GitPullRequestRef(42, "x", "other-branch", PullRequestStateNames.Open));

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        report.Bound.Should().Be(0);
        report.Skipped.Should().Be(1);
        binding.State.PullRequestNumber.Should().BeNull();
    }

    [Fact(DisplayName = "Более одного совпадения head → пропуск (ручная привязка)")]
    public async Task Skips_when_multiple_head_matches()
    {
        var fixture = new Fixture();
        var binding = fixture.SeedReadyBinding();
        fixture.SeedBranch(Branch);
        fixture.SeedOpenPrs(
            new GitPullRequestRef(42, "a", Branch, PullRequestStateNames.Open),
            new GitPullRequestRef(43, "b", Branch, PullRequestStateNames.Open));

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        report.Bound.Should().Be(0);
        report.Skipped.Should().Be(1);
        binding.State.PullRequestNumber.Should().BeNull();
    }

    [Fact(DisplayName = "Не удалось определить ветку клона → пропуск")]
    public async Task Skips_when_branch_unknown()
    {
        var fixture = new Fixture();
        fixture.SeedReadyBinding();
        fixture.SeedBranch(null);

        var report = await fixture.Workflow.RunAsync(CancellationToken.None);

        report.Bound.Should().Be(0);
        report.Skipped.Should().Be(1);
    }

    private sealed class Fixture
    {
        private readonly RecordingUnitOfWork _uow = new();

        public Fixture()
        {
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            var providers = Substitute.For<IGitProviderRegistry>();
            Provider = Substitute.For<IGitProvider>();
            providers.GetByName(GitProviderNames.GitHub).Returns(Provider);
            BranchReader = Substitute.For<ILocalGitBranchReader>();

            var clock = new FixedClock(Now);
            var persistence = new RepositoryBindingPersistence(
                Bindings, Substitute.For<IRepositoryRegistry>(), _uow, clock,
                Substitute.For<IWorkspaceRootProvider>(),
                Substitute.For<IWorkspaceDirectoryRemover>(),
                Substitute.For<IWorkspaceDirectoryProbe>());
            Workflow = new PullRequestAutoBindWorkflow(
                Bindings, providers, BranchReader, persistence,
                NullLogger<PullRequestAutoBindWorkflow>.Instance);

            Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SaveBindingOutcome>(
                    new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public IGitProvider Provider { get; }
        public ILocalGitBranchReader BranchReader { get; }
        public PullRequestAutoBindWorkflow Workflow { get; }

        public IntentRepositoryBinding SeedReadyBinding()
        {
            var binding = IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
                Id: BindingId.New(),
                IntentId: new IntentId("intent-1"),
                Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
                WorkspacePath: "/tmp/throne-test/intent-1/octo__hello",
                DefaultBranch: "main",
                CloneStatus: CloneStatusNames.Ready,
                CloneError: null,
                PullRequestNumber: null,
                PullRequestState: null,
                ReviewCommentsEtag: null,
                LastSeenReviewCommentAt: null,
                LastSyncedAt: null,
                CreatedAt: Now,
                UpdatedAt: Now));
            Bindings.FindReadyWithoutPullRequestAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([binding]));
            return binding;
        }

        public void SeedBranch(string? branch) =>
            BranchReader.ReadCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(branch));

        public void SeedOpenPrs(params GitPullRequestRef[] prs) =>
            Provider.ListPullRequestsAsync("octo", "hello", Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<GitPullRequestRef>>(prs));
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);

        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) =>
            work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
