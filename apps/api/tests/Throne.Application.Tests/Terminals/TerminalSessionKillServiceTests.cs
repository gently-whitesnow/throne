using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Tests.Terminals;

public class TerminalSessionKillServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 10, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-kill-1";
    private const string WorkspaceRoot = "/tmp/throne-test-workspaces";

    [Fact(DisplayName = "Kill падает с capability.disabled и не трогает tmux, если terminal выключен")]
    public async Task Kill_capability_disabled_throws()
    {
        var fixture = new Fixture().Setup(intentExists: true);

        var act = () => fixture.Service.KillAsync(IntentIdValue, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityDisabled);
        await fixture.Tmux.DidNotReceive().KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Kill на отсутствующий intent → intent.not_found")]
    public async Task Kill_missing_intent_throws()
    {
        var fixture = new Fixture().Setup(capabilityEnabled: true);

        var act = () => fixture.Service.KillAsync(IntentIdValue, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.IntentNotFound);
    }

    [Fact(DisplayName = "Kill зовёт kill-session и возвращает exited со снапшотом биндингов")]
    public async Task Kill_kills_session_and_returns_exited()
    {
        var binding = NewBinding(cloneStatus: CloneStatusNames.Ready);
        var fixture = new Fixture().Setup(
            capabilityEnabled: true,
            intentExists: true,
            bindings: [binding]);
        fixture.Tmux.KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>()).Returns(true);

        var result = await fixture.Service.KillAsync(IntentIdValue, CancellationToken.None);

        result.SessionState.Should().Be(TerminalSessionStates.Exited);
        result.SessionName.Should().Be($"throne-{IntentIdValue}");
        result.Bindings.Should().ContainSingle().Which.BindingId.Should().Be(binding.Id.Value);
        await fixture.Tmux.Received(1).KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>());
        await fixture.Tmux.DidNotReceive().SpawnAsync(Arg.Any<TmuxSpawnRequest>(), Arg.Any<CancellationToken>());
    }

    private static IntentRepositoryBinding NewBinding(string cloneStatus)
    {
        var snapshot = new IntentRepositoryBindingSnapshot(
            Id: BindingId.New(),
            IntentId: new IntentId(IntentIdValue),
            Coordinate: new RepoCoordinate(GitProviderNames.GitHub, "octo", "hello"),
            WorkspacePath: $"{WorkspaceRoot}/intents/{IntentIdValue}/octo__hello",
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
            Tmux = Substitute.For<ITmuxSessionManager>();
            var options = new RunPreflightOptions();
            var waiter = new TmuxTuiReadinessWaiter(
                Tmux, options, TimeProvider.System, new TerminalReadinessSignals(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<TmuxTuiReadinessWaiter>.Instance);
            var spawn = new RunPreflightSpawn(
                Tmux,
                new StubWorkspaceRoot(WorkspaceRoot),
                Substitute.For<IWorkspaceTrust>(),
                Array.Empty<ISessionHookAdapter>(),
                waiter,
                options,
                new SetIntentStatusHandler(Intents, new PassthroughUnitOfWork(), new FixedClock(Now)),
                Substitute.For<IDomainEventDispatcher>());
            var guards = new RunPreflightGuards(Intents, Capabilities, spawn);
            Service = new TerminalSessionKillService(guards, Bindings, spawn);
        }

        public IIntentRepository Intents { get; }
        public ICapabilitiesRepository Capabilities { get; }
        public IIntentRepositoryBindingRepository Bindings { get; }
        public ITmuxSessionManager Tmux { get; }
        public TerminalSessionKillService Service { get; }

        public Fixture Setup(
            bool capabilityEnabled = false,
            bool intentExists = false,
            IReadOnlyList<IntentRepositoryBinding>? bindings = null)
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
            }
            else
            {
                Intents.GetByIdAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                    .Returns(Task.FromResult<Intent?>(null));
            }

            Bindings.FindByIntentAsync(Arg.Any<IntentId>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(bindings ?? []));

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
