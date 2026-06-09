using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Terminals;
using Throne.Domain.Intents;

namespace Throne.Application.Tests.Terminals;

/// <summary>
/// ADR-0026 § 8: the tmux session is killed when an intent reaches <c>done</c> (both the
/// PR-merge auto-close and a manual «done»), and left alone for every other status.
/// </summary>
public class TerminalKillOnIntentDoneHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-1";

    [Fact(DisplayName = "IntentStatusChanged → done: tmux-сессия убивается")]
    public async Task Kills_session_on_done()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(IntentStatusNames.Done), CancellationToken.None);

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

    [Fact(DisplayName = "IntentStatusChanged → reject: сессия не трогается")]
    public async Task Does_not_kill_on_reject()
    {
        var fixture = new Fixture();

        await fixture.Handler.HandleAsync(StatusEvent(IntentStatusNames.Reject), CancellationToken.None);

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
            Handler = new TerminalKillOnIntentDoneHandler(
                new Lazy<ITmuxSessionManager>(() => Tmux),
                NullLogger<TerminalKillOnIntentDoneHandler>.Instance);
        }

        public ITmuxSessionManager Tmux { get; }
        public TerminalKillOnIntentDoneHandler Handler { get; }
    }
}
