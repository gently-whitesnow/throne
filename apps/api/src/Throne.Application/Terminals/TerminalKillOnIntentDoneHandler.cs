using Throne.Application.Events;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Kills the per-intent tmux session when an intent transitions to <c>done</c> (ADR-0026 § 8,
/// which supersedes the original § 7 «no status → kill tmux» decision). Fires for both the
/// PR-merge auto-close and the operator's manual «закрыть как готово» — both land on the
/// same <see cref="IntentStatusChanged"/> event. <c>reject</c>/<c>fridge</c> are left alone.
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
public sealed class TerminalKillOnIntentDoneHandler(Lazy<ITmuxSessionManager> tmux) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        if (evt is not IntentStatusChanged changed ||
            !string.Equals(changed.Intent.State.Status, IntentStatusNames.Done, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            await tmux.Value.KillSessionAsync(changed.Intent.Id.Value, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Best-effort: liveness is owned by tmux, not this hook (ADR-0026 § 2).
        }
    }
}
