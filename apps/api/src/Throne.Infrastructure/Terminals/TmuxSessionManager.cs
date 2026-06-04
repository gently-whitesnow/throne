using Microsoft.Extensions.Logging;
using Throne.Application.Events;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Stateless adapter over the tmux CLI (see <see cref="ITmuxSessionManager"/>). Every call
/// shells out and returns the freshly observed result — there is no in-process cache so
/// liveness can never drift from what <c>tmux has-session</c> would report (ADR-0026 § 2).
/// </summary>
internal sealed class TmuxSessionManager(
    TmuxCli tmux,
    ILogger<TmuxSessionManager> log,
    IDomainEventDispatcher events)
    : ITmuxSessionManager
{
    public async Task<TmuxSpawnResult> SpawnAsync(TmuxSpawnRequest request, CancellationToken ct)
    {
        var sessionName = TmuxSessionName.For(request?.IntentId ?? string.Empty);
        var args = TmuxSpawnArgsBuilder.Build(sessionName, request!);

        var outcome = await tmux.RunAsync(args, ct);
        var detail = TmuxOutcomeDetail.Extract(outcome);

        SpawnOutcomeReporter.Report(log, sessionName, outcome, detail);

        var alive = outcome.IsAvailable && await HasSessionAsync(request!.IntentId, ct);
        return new TmuxSpawnResult(sessionName, alive, detail);
    }

    public async Task<bool> HasSessionAsync(string intentId, CancellationToken ct)
    {
        var sessionName = TmuxSessionName.For(intentId);
        var outcome = await tmux.RunAsync(["has-session", "-t", sessionName], ct);
        return outcome.IsSuccess;
    }

    public async Task<bool> KillSessionAsync(string intentId, CancellationToken ct)
    {
        var sessionName = TmuxSessionName.For(intentId);
        var outcome = await tmux.RunAsync(["kill-session", "-t", sessionName], ct);
        if (!outcome.IsSuccess)
        {
            return false;
        }

        await events.DispatchAsync(new TerminalSessionStopped(intentId), ct);
        return true;
    }

    public async Task<IReadOnlyList<string>> ListThroneSessionsAsync(CancellationToken ct)
    {
        var outcome = await tmux.RunAsync(["list-sessions", "-F", "#S"], ct);
        return TmuxSessionListParser.ParseThroneSessions(outcome);
    }

    public async Task SendLiteralTextAsync(string intentId, string text, CancellationToken ct)
    {
        var sessionName = TmuxSessionName.For(intentId);
        // -l: treat the argument as literal UTF-8 text, not a key name; no trailing Enter.
        await tmux.RunAsync(["send-keys", "-t", sessionName, "-l", text], ct);
    }
}
