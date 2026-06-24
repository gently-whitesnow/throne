using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Application.Terminals;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class PullRequestAutoBindOnStopSubscriberTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";
    private const string Branch = "work/pr";
    private const string LiveRoot = "/tmp/throne-live-root";

    [Fact(DisplayName = "Non-Stop hook не запускает поиск binding'ов")]
    public async Task Ignores_non_stop_hooks()
    {
        var fixture = new Fixture();

        await fixture.Subscriber.HandleAsync(
            new TerminalHookEvent(
                IntentIdValue,
                TerminalHookEvents.UserPromptSubmit,
                TerminalRunModes.Work,
                Now),
            CancellationToken.None);

        await fixture.Bindings.DidNotReceiveWithAnyArgs()
            .FindByIntentAsync(default!, default);
    }

    [Fact(DisplayName = "Stop hook с уже привязанным PR не дергает provider")]
    public async Task Skips_bindings_that_already_have_pull_request()
    {
        var fixture = new Fixture();
        fixture.SeedBindings(Fixture.ReadyBinding(pullRequestNumber: 7));

        await fixture.Subscriber.HandleAsync(StopHook(), CancellationToken.None);

        await fixture.BranchReader.DidNotReceiveWithAnyArgs()
            .ReadCurrentBranchAsync(default!, default);
        await fixture.Provider.DidNotReceiveWithAnyArgs()
            .ListPullRequestsAsync(default!, default!, default, default, default);
    }

    [Fact(DisplayName = "Stop hook запускает auto-bind для ready-binding без PR")]
    public async Task Runs_auto_bind_for_ready_binding_without_pull_request()
    {
        var fixture = new Fixture();
        var binding = Fixture.ReadyBinding(pullRequestNumber: null);
        fixture.SeedBindings(binding);
        fixture.SeedBranch(Branch);
        fixture.SeedOpenPrs(new GitPullRequestRef(42, "title", Branch, PullRequestStateNames.Open));

        await fixture.Subscriber.HandleAsync(StopHook(), CancellationToken.None);

        binding.State.PullRequestNumber.Should().Be(42);
        await fixture.Bindings.Received(1).SaveAsync(
            Arg.Is<IntentRepositoryBinding>(b => b.State.PullRequestNumber == 42),
            Arg.Any<CancellationToken>());
    }

    private static TerminalHookEvent StopHook() =>
        new(IntentIdValue, TerminalHookEvents.Stop, TerminalRunModes.Work, Now);

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
            var workspace = Substitute.For<IWorkspaceRootProvider>();
            workspace.ResolvedRoot.Returns(LiveRoot);
            var persistence = new RepositoryBindingPersistence(
                Bindings,
                Substitute.For<IRepositoryRegistry>(),
                _uow,
                new FixedClock(Now),
                workspace,
                Substitute.For<IWorkspaceDirectoryRemover>(),
                Substitute.For<IWorkspaceDirectoryProbe>());
            var workflow = new PullRequestAutoBindWorkflow(
                Bindings,
                providers,
                BranchReader,
                persistence,
                workspace,
                NullLogger<PullRequestAutoBindWorkflow>.Instance);
            Subscriber = new PullRequestAutoBindOnStopSubscriber(
                Bindings,
                workflow,
                NullLogger<PullRequestAutoBindOnStopSubscriber>.Instance);

            Bindings.SaveAsync(Arg.Any<IntentRepositoryBinding>(), Arg.Any<CancellationToken>())
                .Returns(ci => Task.FromResult<SaveBindingOutcome>(
                    new SaveBindingOutcome.Saved(ci.Arg<IntentRepositoryBinding>())));
        }

        public IIntentRepositoryBindingRepository Bindings { get; }
        public IGitProvider Provider { get; }
        public ILocalGitBranchReader BranchReader { get; }
        public PullRequestAutoBindOnStopSubscriber Subscriber { get; }

        public void SeedBindings(params IntentRepositoryBinding[] bindings) =>
            Bindings.FindByIntentAsync(new IntentId(IntentIdValue), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(bindings));

        public static IntentRepositoryBinding ReadyBinding(int? pullRequestNumber) =>
            IntentRepositoryBinding.Restore(new IntentRepositoryBindingSnapshot(
                Id: BindingId.New(),
                IntentId: new IntentId(IntentIdValue),
                Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
                WorkspacePath: "/stale/path",
                DefaultBranch: "main",
                CloneStatus: CloneStatusNames.Ready,
                CloneError: null,
                PullRequestNumber: pullRequestNumber,
                PullRequestState: null,
                ReviewCommentsEtag: null,
                LastSeenReviewCommentAt: null,
                LastSyncedAt: null,
                CreatedAt: Now,
                UpdatedAt: Now));

        public void SeedBranch(string branch) =>
            BranchReader.ReadCurrentBranchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<string?>(branch));

        public void SeedOpenPrs(params GitPullRequestRef[] prs) =>
            Provider.ListPullRequestsAsync(
                    "octo",
                    "hello",
                    Arg.Any<string?>(),
                    Arg.Any<int>(),
                    Arg.Any<CancellationToken>())
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
