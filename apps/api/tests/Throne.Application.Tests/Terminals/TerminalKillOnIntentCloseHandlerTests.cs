using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Terminals;

/// <summary>
/// ADR-0026 § 8: the tmux session is killed when an intent is closed — <c>done</c> or <c>reject</c>
/// (both the PR-merge auto-close and a manual transition) — and left alone for every other status.
/// <c>fridge</c> is a pause, not a close, so its session survives.
/// </summary>
public class TerminalKillOnIntentCloseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";

    [Theory(DisplayName = "IntentStatusChanged → done|reject: tmux-сессия убивается")]
    [InlineData(IntentStatusNames.Done)]
    [InlineData(IntentStatusNames.Reject)]
    public async Task Kills_session_on_close(string status)
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(status), CancellationToken.None);

        await fixture.Tmux.Received(1).KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Перед kill зовётся has-session для pre-kill snapshot в логе")]
    public async Task Probes_has_session_before_kill()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(IntentStatusNames.Done), CancellationToken.None);

        Received.InOrder(() =>
        {
            fixture.Tmux.HasSessionAsync(IntentIdValue, Arg.Any<CancellationToken>());
            fixture.Tmux.KillSessionAsync(IntentIdValue, Arg.Any<CancellationToken>());
        });
    }

    [Theory(DisplayName = "close c cleanup_local_state_on_done=false: сессия не трогается (единый teardown)")]
    [InlineData(IntentStatusNames.Done)]
    [InlineData(IntentStatusNames.Reject)]
    public async Task Does_not_kill_when_gate_off(string status)
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(
            new IntentStatusChanged(Intent.Restore(
                new IntentId(IntentIdValue), "x", status, 1, [], Now, Now,
                cleanupLocalStateOnDone: false)),
            CancellationToken.None);

        await fixture.Tmux.DidNotReceive().KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "IntentStatusChanged → fridge: заморозка, сессия не трогается")]
    public async Task Does_not_kill_on_fridge()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(IntentStatusNames.Fridge), CancellationToken.None);

        await fixture.Tmux.DidNotReceive().KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Не-status событие игнорируется")]
    public async Task Ignores_other_events()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(
            new IntentCreated(Intent.Restore(new IntentId(IntentIdValue), "x", IntentStatusNames.Done, 1, [], Now, Now)),
            CancellationToken.None);

        await fixture.Tmux.DidNotReceive().KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static IntentStatusChanged StatusEvent(string status) =>
        new(Intent.Restore(new IntentId(IntentIdValue), "x", status, 1, [], Now, Now));

    private sealed class Fixture
    {
        public Fixture()
        {
            Tmux = Substitute.For<ITmuxSessionManager>();
            Tmux.KillSessionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
            Handler = new TerminalKillOnIntentCloseHandler(
                new Lazy<ITmuxSessionManager>(() => Tmux),
                NullLogger<TerminalKillOnIntentCloseHandler>.Instance);
        }

        public ITmuxSessionManager Tmux { get; }
        public TerminalKillOnIntentCloseHandler Handler { get; }
    }
}
