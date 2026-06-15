using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Repositories;
using Throne.Application.Terminals;
using Throne.Application.Tests.Manifest;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Tests.Terminals;

public partial class RunPreflightOrchestratorTests
{
    private static IntentRepositoryBinding NewBinding(
        string cloneStatus = CloneStatusNames.Pending,
        string owner = "octo",
        string repo = "hello")
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(IntentIdValue),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, owner, repo),
            WorkspacePath: $"{WorkspaceRoot}/intents/{IntentIdValue}/{owner}__{repo}",
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

    private sealed class Fixture
    {
        public Fixture()
        {
            Intents = Substitute.For<IIntentRepository>();
            Capabilities = Substitute.For<ICapabilitiesRepository>();
            Bindings = Substitute.For<IIntentRepositoryBindingRepository>();
            Tags = Substitute.For<ITagRepository>();
            Tmux = Substitute.For<ITmuxSessionManager>();
            var workspace = new StubWorkspaceRoot(WorkspaceRoot);
            var clock = new FixedClock(Now);
            var uow = new PassthroughUnitOfWork();
            var (bindingService, cloneQueue) = BuildBindingService(workspace, clock, uow);
            var transitions = new RepositoryCloneTransitionWriter(Bindings, uow, clock);
            var autoBind = new RunPreflightAutoBind(new TagDefaultsUnion(Tags), Bindings, bindingService);
            var queue = new RunPreflightCloneScheduler(Bindings, cloneQueue, transitions);
            var runPreflightOptions = new RunPreflightOptions
            {
                TuiReadinessTimeoutMilliseconds = 200,
                TuiReadinessPollIntervalMilliseconds = 20,
            };
            var cloneWait = new RunPreflightCloneWait(Bindings, runPreflightOptions, clock);
            var spawn = BuildSpawn(workspace, clock, uow, runPreflightOptions);
            var guards = new RunPreflightGuards(Intents, Capabilities, spawn);
            var launchResolver = BuildLaunchResolver();
            var promptGate = BuildPromptGate(clock, uow);
            Orchestrator = new RunPreflightOrchestrator(
                guards, autoBind, queue, cloneWait, spawn, promptGate, launchResolver);
        }

        private (RepositoryBindingService Service, IRepositoryCloneRequests CloneQueue) BuildBindingService(
            IWorkspaceRootProvider workspace, TimeProvider clock, IUnitOfWork uow)
        {
            // BindingService is only invoked from the autobind path; in these tests we never seed
            // tag defaults so the call site is unreachable, but the sealed service still needs
            // wiring — feed it stub collaborators that throw if exercised.
            var resolver = new RepositoryBindingResolver(Intents, Bindings, Substitute.For<IGitProviderRegistry>());
            var persistence = new RepositoryBindingPersistence(
                Bindings, Substitute.For<IRepositoryRegistry>(), uow, clock, workspace,
                Substitute.For<IWorkspaceDirectoryRemover>(),
                Substitute.For<IWorkspaceDirectoryProbe>());
            var syncPersistence = new RepositoryPullRequestSyncPersistence(Bindings, uow, clock);
            var autoCloser = new IntentMergeAutoCloser(
                Bindings, Substitute.For<ISystemIntentStatusWriter>(), uow, clock,
                NullLogger<IntentMergeAutoCloser>.Instance);
            var stateRefresher = new PullRequestStateRefresher(
                Bindings, uow, autoCloser, clock, NullLogger<PullRequestStateRefresher>.Instance);
            var syncWorkflow = new RepositoryPullRequestSyncWorkflow(syncPersistence, stateRefresher);
            var cloneQueue = Substitute.For<IRepositoryCloneRequests>();
            var autoBindWorkflow = new PullRequestAutoBindWorkflow(
                Bindings,
                Substitute.For<IGitProviderRegistry>(),
                Substitute.For<ILocalGitBranchReader>(),
                persistence);
            var service = new RepositoryBindingService(
                resolver, persistence, syncWorkflow,
                new RepositoryCloneTransitionWriter(Bindings, uow, clock), cloneQueue, autoBindWorkflow);
            return (service, cloneQueue);
        }

        private RunPreflightSpawn BuildSpawn(
            IWorkspaceRootProvider workspace, TimeProvider clock, IUnitOfWork uow, RunPreflightOptions options)
        {
            // FixedClock would hang the readiness poll loop, so feed the waiter a System TimeProvider.
            // Happy-path tests stub CapturePaneAsync to a composer marker so the waiter resolves on first capture.
            Tmux.CapturePaneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("│ > ready");
            var readinessWaiter = new TmuxTuiReadinessWaiter(
                Tmux, options, TimeProvider.System, NullLogger<TmuxTuiReadinessWaiter>.Instance);
            return new RunPreflightSpawn(
                Tmux, workspace, Substitute.For<IWorkspaceTrust>(),
                [new StubHookAdapter(TerminalAgentCatalog.VendorClaude, ["--settings", SettingsPath])],
                readinessWaiter, options,
                new SetIntentStatusHandler(Intents, uow, clock),
                Substitute.For<IDomainEventDispatcher>());
        }

        private static TerminalLaunchResolver BuildLaunchResolver()
        {
            var settingsStore = Substitute.For<ITerminalSettingsStore>();
            settingsStore.GetDefaultVendorAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(TerminalAgentCatalog.VendorClaude));
            return new TerminalLaunchResolver(settingsStore);
        }

        private RunPreflightPromptGate BuildPromptGate(TimeProvider clock, IUnitOfWork uow)
        {
            var promptPartsRepo = Substitute.For<IPromptPartRepository>();
            var promptResolver = new PromptCompositionResolver(
                SkillManifestFixtures.Provider(),
                new PromptBundleResolver(promptPartsRepo),
                promptPartsRepo);
            return new RunPreflightPromptGate(
                promptResolver, new ReplaceIntentTextHandler(Intents, uow, clock));
        }

        public IIntentRepository Intents { get; }
        public ICapabilitiesRepository Capabilities { get; }
        public IIntentRepositoryBindingRepository Bindings { get; }
        public ITagRepository Tags { get; }
        public ITmuxSessionManager Tmux { get; }
        public RunPreflightOrchestrator Orchestrator { get; }

        public Fixture Setup(
            bool capabilityEnabled = false,
            bool intentExists = false,
            bool? hasSession = null,
            IReadOnlyList<IntentRepositoryBinding>? bindings = null,
            TmuxSpawnResult? spawn = null)
        {
            if (capabilityEnabled)
            {
                var stored = CapabilitiesAggregate.CreateEmpty(Now);
                stored.SetEnabled(CapabilityNames.Terminal, true, Now);
                Capabilities.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CapabilitiesAggregate?>(stored));
            }
            else
            {
                Capabilities.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CapabilitiesAggregate?>(null));
            }

            if (intentExists)
            {
                var intent = Intent.Restore(
                    new IntentId(IntentIdValue), "x", IntentStatusNames.Work, 1, [], Now, Now);
                Intents.GetByIdAsync(Arg.Is<IntentId>(i => i.Value == IntentIdValue), Arg.Any<CancellationToken>())
                    .Returns(intent);
                Intents.SetStatusAsync(
                        Arg.Any<IntentId>(),
                        Arg.Any<string>(),
                        Arg.Any<string?>(),
                        Arg.Any<string?>(),
                        Arg.Any<IntentTrainingAuthor>(),
                        Arg.Any<string>(),
                        Arg.Any<DateTimeOffset>(),
                        Arg.Any<CancellationToken>())
                    .Returns(ci => new SetIntentStatusOutcome.Updated(
                        Intent.Restore(ci.ArgAt<IntentId>(0), "x", ci.ArgAt<string>(1), 1, [], Now, Now)));
            }
            else
            {
                Intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Intent?>(null));
            }

            if (hasSession is { } alive)
            {
                Tmux.HasSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(alive);
            }

            if (bindings is not null)
            {
                Bindings.FindByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult(bindings));
            }

            if (spawn is not null)
            {
                Tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>()).Returns(spawn);
            }

            return this;
        }
    }

    private sealed class StubWorkspaceRoot(string root) : IWorkspaceRootProvider
    {
        public string ResolvedRoot { get; } = root;
    }

    private sealed class StubHookAdapter(string vendor, IReadOnlyList<string> args) : ISessionHookAdapter
    {
        public string Vendor => vendor;

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId, string workspacePath, string mode, string? systemPrompt, CancellationToken ct) =>
            Task.FromResult(args);

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

        public bool IsTuiReady(string paneSnapshot) =>
            !string.IsNullOrEmpty(paneSnapshot)
            && paneSnapshot.Contains("│ >", StringComparison.Ordinal);
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
