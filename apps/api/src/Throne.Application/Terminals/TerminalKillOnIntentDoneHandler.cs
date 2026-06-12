using Microsoft.Extensions.Logging;
using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Kills the per-intent tmux session when an intent transitions to <c>done</c> (ADR-0026 § 8,
/// which supersedes the original § 7 «no status → kill tmux» decision). Fires for both the
/// PR-merge auto-close and the operator's manual «закрыть как готово» — both land on the
/// same <see cref="IntentStatusChanged"/> event. <c>reject</c>/<c>fridge</c> are left alone.
///
/// Gated by <see cref="IntentState.CleanupLocalStateOnDone"/> (default true) so terminal-stop is
/// one half of a single teardown-on-done decision, the sibling of the workspace/trust wipe in
/// <c>IntentLocalStateCleanupOnDoneHandler</c>: clearing the gate keeps both the session and the
/// local state alive past <c>done</c>, regardless of which path reached it.
///
/// Best-effort and idempotent: a missing session is a silent no-op, and a tmux failure is
/// swallowed so it never aborts the post-commit event fan-out. tmux remains the single source
/// of truth for liveness — a missed kill self-heals on the next <c>/intents/contexts</c> refresh.
///
/// <see cref="ITmuxSessionManager"/> is taken via <see cref="Lazy{T}"/>: the manager itself
/// depends on <c>IDomainEventDispatcher</c>, which depends on the full
/// <c>IEnumerable&lt;IDomainEventHandler&gt;</c> — eager injection here would close that
/// resolution cycle (same pattern as <c>Lazy&lt;IUnitOfWork&gt;</c> in the composition root).
/// </summary>
public sealed partial class TerminalKillOnIntentDoneHandler(
    Lazy<ITmuxSessionManager> tmux,
    ILogger<TerminalKillOnIntentDoneHandler> logger) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        if (evt is not IntentStatusChanged changed ||
            !string.Equals(changed.Intent.State.Status, IntentStatusNames.Done, StringComparison.Ordinal))
        {
            return;
        }

        var intentId = changed.Intent.Id.Value;
        if (!changed.Intent.State.CleanupLocalStateOnDone)
        {
            LogSkipDisabled(logger, intentId);
            return;
        }

        try
        {
            var manager = tmux.Value;
            var preAlive = await manager.HasSessionAsync(intentId, ct);
            LogInvoked(logger, intentId, changed.Intent.State.Status, preAlive);
            var killed = await manager.KillSessionAsync(intentId, ct);
            if (killed)
            {
                LogKilled(logger, intentId);
            }
            else
            {
                LogNoSession(logger, intentId, preAlive);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort: liveness is owned by tmux, not this hook (ADR-0026 § 2).
            // We still log so a survived session is diagnosable instead of silent.
            LogKillFailed(logger, intentId, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "TerminalKillOnIntentDone: killed tmux session for intent {IntentId} (status -> done).")]
    private static partial void LogKilled(ILogger logger, string intentId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "TerminalKillOnIntentDone: no tmux session killed for intent {IntentId} "
            + "(pre_alive={PreAlive}) — either none existed or `tmux kill-session` returned non-zero. "
            + "Intent reached done but its session may still be alive — see tmux kill-session log "
            + "with same intent id for exit_code / stderr / post_alive.")]
    private static partial void LogNoSession(ILogger logger, string intentId, bool preAlive);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "TerminalKillOnIntentDone: tmux kill threw for intent {IntentId} — "
            + "swallowed (best-effort), session may still be alive.")]
    private static partial void LogKillFailed(ILogger logger, string intentId, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "TerminalKillOnIntentDone: invoked for intent {IntentId} (status={Status}, pre_alive={PreAlive}).")]
    private static partial void LogInvoked(ILogger logger, string intentId, string status, bool preAlive);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "TerminalKillOnIntentDone: intent {IntentId} terminal kill skipped — gate off "
            + "(cleanup_local_state_on_done=false).")]
    private static partial void LogSkipDisabled(ILogger logger, string intentId);
}
