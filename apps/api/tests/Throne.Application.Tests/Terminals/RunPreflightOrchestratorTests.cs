using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Application.Terminals;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Tests.Terminals;

public class RunPreflightOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 10, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-run-1";
    private const string WorkspaceRoot = "/tmp/throne-test-workspaces";

    [Fact(DisplayName = "Run падает с capability.disabled, если capability terminal выключен")]
    public async Task Run_capability_disabled_throws()
    {
        var fixture = new Fixture().Setup(intentExists: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityDisabled);
        await fixture.Tmux.DidNotReceive().SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run на неизвестный mode бросает terminal.mode_invalid")]
    public async Task Run_invalid_mode_throws()
    {
        var fixture = new Fixture().Setup(capabilityEnabled: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, "interactive", restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalModeInvalid);
    }

    [Fact(DisplayName = "Run без живой сессии и готовых клонов спавнит tmux и возвращает running")]
    public async Task Run_spawns_when_all_bindings_ready()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Work, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        result.SessionName.Should().Be($"throne-{IntentIdValue}");
        result.BlockingBindings.Should().BeEmpty();
        await fixture.Tmux.Received(1).SpawnAsync(
            Arg.Is<TmuxSpawnRequest>(r =>
                r.Command == "claude"
                && r.Arguments.Single().Contains(IntentIdValue)
                && r.Arguments.Single().Contains("бандл work")
                && r.WorkingDirectory.EndsWith($"intents/{IntentIdValue}")),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Free-режим спавнит claude без prompt-аргумента и предзаполняет терминал текстом")]
    public async Task Run_free_mode_pretypes_prompt()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Free, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        await fixture.Tmux.Received(1).SpawnAsync(
            Arg.Is<TmuxSpawnRequest>(r => r.Command == "claude" && r.Arguments.Count == 0),
            Arg.Any<CancellationToken>());
        await fixture.Tmux.Received(1).SendLiteralTextAsync(
            IntentIdValue,
            $"прочитай интент {IntentIdValue} и <твой вопрос>",
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run с broken-binding возвращает blocked и не спавнит tmux")]
    public async Task Run_blocked_when_binding_broken()
    {
        var broken = NewBinding(cloneStatus: CloneStatusNames.Broken);
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [broken]);

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Work, restart: false, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Blocked);
        result.BlockingBindings.Should().ContainSingle().Which.Should().Be(broken.Id.Value);
        await fixture.Tmux.DidNotReceive().SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Run при живой сессии без restart=true бросает terminal.session_already_running")]
    public async Task Run_existing_session_throws_409()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalSessionAlreadyRunning);
    }

    [Fact(DisplayName = "Restart=true сначала kill-session, затем спавнит заново")]
    public async Task Restart_kills_then_spawns()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: true,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult(TmuxSessionName.For(IntentIdValue), IsAlive: true, Detail: null));
        fixture.Tmux.KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>()).Returns(true);

        var result = await fixture.Orchestrator.RunAsync(
            IntentIdValue, TerminalRunModes.Interview, restart: true, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Running);
        Received.InOrder(() =>
        {
            fixture.Tmux.KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>());
            fixture.Tmux.SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Run на отсутствующий intent → intent.not_found")]
    public async Task Run_missing_intent_throws()
    {
        var fixture = new Fixture().Setup(capabilityEnabled: true);

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "Spawn вернул IsAlive=false → terminal.spawn_failed")]
    public async Task Run_spawn_not_alive_throws()
    {
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            hasSession: false,
            bindings: [NewBinding(cloneStatus: CloneStatusNames.Ready)],
            spawn: new TmuxSpawnResult($"throne-{IntentIdValue}", IsAlive: false, Detail: "tmux: command not found"));

        var act = () => fixture.Orchestrator.RunAsync(IntentIdValue, TerminalRunModes.Work, restart: false, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.TerminalSpawnFailed);
    }

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
                Bindings, uow, clockShared, workspace, Substitute.For<IWorkspaceDirectoryRemover>());
            var syncPersistence = new RepositoryPullRequestSyncPersistence(Bindings, uow, clockShared);
            var stateRefresher = new PullRequestStateRefresher(Bindings, uow, clockShared);
            var syncWorkflow = new RepositoryPullRequestSyncWorkflow(syncPersistence, stateRefresher);
            var cloneQueue = Substitute.For<IRepositoryCloneRequests>();
            var bindingService = new RepositoryBindingService(
                resolver,
                persistence,
                syncWorkflow,
                cloneQueue);

            var union = new TagDefaultsUnion(Tags);
            var transitions = new RepositoryCloneTransitionWriter(Bindings, uow, clockShared);
            var autoBind = new RunPreflightAutoBind(union, Bindings, bindingService);
            var queue = new RunPreflightCloneScheduler(Bindings, cloneQueue, transitions);
            var cloneWait = new RunPreflightCloneWait(Bindings, new RunPreflightOptions(), clockShared);
            var spawn = new RunPreflightSpawn(Tmux, workspace);
            var guards = new RunPreflightGuards(Intents, Capabilities, spawn);
            Orchestrator = new RunPreflightOrchestrator(guards, autoBind, queue, cloneWait, spawn);
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
                    new IntentId(IntentIdValue), "user-1", "x", IntentStatusNames.Work, 1, [], Now, Now);
                Intents.GetByIdAsync(Arg.Is<IntentId>(i => i.Value == IntentIdValue), Arg.Any<CancellationToken>())
                    .Returns(intent);
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
