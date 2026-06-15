using System.Diagnostics;
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

        // Fail clearly before tmux returns its opaque "command too long" / exit 1.
        if (TmuxCommandLimit.Exceeds(args, out var limitDetail))
        {
            TerminalsLog.TmuxSpawnFailed(log, sessionName, -1, limitDetail);
            return new TmuxSpawnResult(sessionName, false, limitDetail);
        }

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
        var preAlive = await HasSessionAsync(intentId, ct);
        var sw = Stopwatch.StartNew();
        var outcome = await tmux.RunAsync(["kill-session", "-t", sessionName], ct);
        sw.Stop();

        if (!outcome.IsCompleted)
        {
            TerminalsLog.TmuxKillUnavailable(
                log, sessionName, outcome.BinaryMissingDetail ?? outcome.FailureDetail ?? "<no detail>");
            return false;
        }

        var result = outcome.Result!;
        var postAlive = await HasSessionAsync(intentId, ct);
        TerminalsLog.TmuxKillResult(
            log,
            sessionName,
            preAlive,
            result.ExitCode,
            sw.ElapsedMilliseconds,
            string.IsNullOrEmpty(result.StandardError) ? "<empty>" : result.StandardError.Trim(),
            postAlive);

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

    public async Task PasteFileAsSubmittedPromptAsync(string intentId, string filePath, CancellationToken ct)
    {
        var sessionName = TmuxSessionName.For(intentId);
        var bufferName = sessionName + "-task";

        // load-buffer: tmux server reads the file directly — the argv stays tiny no matter the
        // payload size. paste-buffer: -d deletes the buffer afterwards (no leak between runs),
        // -p wraps the bytes in bracketed-paste markers so claude/codex TUIs treat embedded
        // \n as paste content, not as Enter. The final send-keys Enter is the single submit.
        await tmux.RunAsync(["load-buffer", "-b", bufferName, filePath], ct);
        await tmux.RunAsync(["paste-buffer", "-d", "-p", "-b", bufferName, "-t", sessionName], ct);
        await tmux.RunAsync(["send-keys", "-t", sessionName, "Enter"], ct);
    }

    public async Task<string> CapturePaneAsync(string intentId, CancellationToken ct)
    {
        var sessionName = TmuxSessionName.For(intentId);
        // Plain text snapshot (no -e): the readiness predicate matches user-visible glyphs.
        var outcome = await tmux.RunAsync(["capture-pane", "-p", "-t", sessionName], ct);
        if (!outcome.IsSuccess)
        {
            return string.Empty;
        }
        return outcome.Result?.StandardOutput ?? string.Empty;
    }
}
