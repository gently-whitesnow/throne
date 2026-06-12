using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Instructions;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.PromptParts;
using Throne.Application.Repositories;
using Throne.Application.Terminals;
using Throne.Application.Tests.Instructions;
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
            // BindingService is only invoked from the autobind path; in these tests
            // we never seed tag defaults so the call site is unreachable. The real
            // service stays sealed (no NSubstitute proxy), so wire it up with stub
            // collaborators that throw if exercised.
            var providers = Substitute.For<IGitProviderRegistry>();
            var clockShared = new FixedClock(Now);
            var uow = new PassthroughUnitOfWork();
            var resolver = new RepositoryBindingResolver(Intents, Bindings, providers);
            var persistence = new RepositoryBindingPersistence(
                Bindings, Substitute.For<IRepositoryRegistry>(), uow, clockShared, workspace,
                Substitute.For<IWorkspaceDirectoryRemover>(),
                Substitute.For<IWorkspaceDirectoryProbe>());
            var syncPersistence = new RepositoryPullRequestSyncPersistence(Bindings, uow, clockShared);
            var autoCloser = new IntentMergeAutoCloser(
                Bindings,
                Substitute.For<ISystemIntentStatusWriter>(),
                uow,
                clockShared,
                NullLogger<IntentMergeAutoCloser>.Instance);
            var stateRefresher = new PullRequestStateRefresher(
                Bindings, uow, autoCloser, clockShared, NullLogger<PullRequestStateRefresher>.Instance);
            var syncWorkflow = new RepositoryPullRequestSyncWorkflow(syncPersistence, stateRefresher);
            var cloneQueue = Substitute.For<IRepositoryCloneRequests>();
            var bindingService = new RepositoryBindingService(
                resolver,
                persistence,
                syncWorkflow,
                new RepositoryCloneTransitionWriter(Bindings, uow, clockShared),
                cloneQueue);

            var union = new TagDefaultsUnion(Tags);
            var transitions = new RepositoryCloneTransitionWriter(Bindings, uow, clockShared);
            var autoBind = new RunPreflightAutoBind(union, Bindings, bindingService);
            var queue = new RunPreflightCloneScheduler(Bindings, cloneQueue, transitions);
            var cloneWait = new RunPreflightCloneWait(Bindings, new RunPreflightOptions(), clockShared);
            var spawn = new RunPreflightSpawn(
                Tmux,
                workspace,
                Substitute.For<IWorkspaceTrust>(),
                [new StubHookAdapter(TerminalAgentCatalog.VendorClaude, ["--settings", SettingsPath])],
                new SetIntentStatusHandler(Intents, uow, clockShared),
                Substitute.For<IDomainEventDispatcher>());
            var guards = new RunPreflightGuards(Intents, Capabilities, spawn);
            var settingsStore = Substitute.For<ITerminalSettingsStore>();
            settingsStore.GetDefaultVendorAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(TerminalAgentCatalog.VendorClaude));
            var launchResolver = new TerminalLaunchResolver(settingsStore);
            var promptPartsRepo = Substitute.For<IPromptPartRepository>();
            var promptResolver = new PromptCompositionResolver(
                SkillManifestFixtures.Provider(),
                new PromptBundleResolver(promptPartsRepo),
                promptPartsRepo);
            var promptGate = new RunPreflightPromptGate(
                promptResolver, new ReplaceIntentTextHandler(Intents, uow, clockShared));
            Orchestrator = new RunPreflightOrchestrator(
                guards, autoBind, queue, cloneWait, spawn, promptGate, launchResolver);
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
