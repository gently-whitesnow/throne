using Microsoft.Extensions.Logging;
using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Kills the per-intent tmux session when an intent is closed — <c>done</c> or <c>reject</c>
/// (ADR-0026 § 8, which supersedes the original § 7 «no status → kill tmux» decision). Fires for
/// the PR-merge auto-close, the operator's manual «закрыть как готово», and a manual reject — all
/// land on the same <see cref="IntentStatusChanged"/> event. <c>fridge</c> is a pause, not a close,
/// so its session is left alone.
///
/// Gated by <see cref="IntentState.CleanupLocalStateOnClose"/> (default true) so terminal-stop is
/// one half of a single teardown-on-close decision, the sibling of the workspace/trust wipe in
/// <c>IntentLocalStateCleanupOnCloseHandler</c>: clearing the gate keeps both the session and the
/// local state alive past close, regardless of which path reached it.
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
public sealed partial class TerminalKillOnIntentCloseHandler(
    Lazy<ITmuxSessionManager> tmux,
    ILogger<TerminalKillOnIntentCloseHandler> logger) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        if (evt is not IntentStatusChanged changed || !IntentStatusNames.IsClosing(changed.Intent.State.Status))
        {
            return;
        }

        var status = changed.Intent.State.Status;
        var intentId = changed.Intent.Id.Value;
        if (!changed.Intent.State.CleanupLocalStateOnClose)
        {
            LogSkipDisabled(logger, intentId);
            return;
        }

        try
        {
            var manager = tmux.Value;
            var preAlive = await manager.HasSessionAsync(intentId, ct);
            LogInvoked(logger, intentId, status, preAlive);
            var killed = await manager.KillSessionAsync(intentId, ct);
            if (killed)
            {
                LogKilled(logger, intentId, status);
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
        Message = "TerminalKillOnIntentClose: killed tmux session for intent {IntentId} (status -> {Status}).")]
    private static partial void LogKilled(ILogger logger, string intentId, string status);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "TerminalKillOnIntentClose: no tmux session killed for intent {IntentId} "
            + "(pre_alive={PreAlive}) — either none existed or `tmux kill-session` returned non-zero. "
            + "Intent reached a closing status but its session may still be alive — see tmux kill-session log "
            + "with same intent id for exit_code / stderr / post_alive.")]
    private static partial void LogNoSession(ILogger logger, string intentId, bool preAlive);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "TerminalKillOnIntentClose: tmux kill threw for intent {IntentId} — "
            + "swallowed (best-effort), session may still be alive.")]
    private static partial void LogKillFailed(ILogger logger, string intentId, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information,
        Message = "TerminalKillOnIntentClose: invoked for intent {IntentId} (status={Status}, pre_alive={PreAlive}).")]
    private static partial void LogInvoked(ILogger logger, string intentId, string status, bool preAlive);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "TerminalKillOnIntentClose: intent {IntentId} terminal kill skipped — gate off "
            + "(cleanup_local_state_on_done=false).")]
    private static partial void LogSkipDisabled(ILogger logger, string intentId);
}
