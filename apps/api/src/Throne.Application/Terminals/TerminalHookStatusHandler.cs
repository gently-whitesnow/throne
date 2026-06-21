using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Terminals;

/// <summary>
/// Derives intent status from an embedded-terminal hook callback (ADR-0034 §4). Replaces the
/// model-cooperative status-write path in the embedded contour. Two signals park the intent in
/// <c>awaiting_operator</c> — <c>Stop</c> (turn yielded) and <c>Notification</c> (a permission
/// prompt blocks the agent without ending the turn, so Stop never fires) — and two return it to the
/// spawn phase: <c>UserPromptSubmit</c> (operator typed an answer) and <c>PostToolUse</c> (the agent
/// resumed after an approval, which is not a UserPromptSubmit). The mapping is deterministic and
/// stateless — the spawn phase rides in on the hook URL, no session-mode store. Idempotent by
/// construction (repeated set to the same status is a no-op at the domain level), so PostToolUse
/// firing on every tool call is harmless.
/// </summary>
public sealed class TerminalHookStatusHandler(IIntentRepository repository, SetIntentStatusHandler setStatus)
{
    private const string SourcePrefix = "hook:terminal:";

    public async Task HandleAsync(string intentId, string hookEvent, string? mode, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hookEvent);

        var target = ResolveTargetStatus(hookEvent, mode);
        if (target is null)
        {
            return;
        }

        // A late Stop hook must not resurrect an intent the operator has already closed: once the
        // run reached a terminal status, the embedded contour no longer owns its lifecycle.
        var intent = await repository.GetByIdAsync(new IntentId(intentId), ct);
        if (intent is null || IntentStatusNames.IsTerminal(intent.State.Status))
        {
            return;
        }

        await setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intentId,
                target,
                Reason: null,
                IntentTrainingAuthor.System,
                SourcePrefix + hookEvent),
            ct);
    }

    /// <summary>
    /// Maps (event, spawn mode) to the intent status the hook drives, or <c>null</c> when the hook
    /// is a no-op for status. Bundle-less <c>dream</c> never touches the status machine — it runs
    /// without an intent and has no phase to park or return to. <c>free</c> and <c>review</c> are
    /// phased like work (spawn→work, Stop→awaiting_operator, UserPromptSubmit→work); only their
    /// injected context differs.
    /// </summary>
    private static string? ResolveTargetStatus(string hookEvent, string? mode) => hookEvent switch
    {
        // Park: turn yielded (Stop) or blocked on a permission prompt (Notification).
        TerminalHookEvents.Stop or TerminalHookEvents.Notification when IsPhasedMode(mode)
            => IntentStatusNames.AwaitingOperator,
        // Return to the spawn phase: operator answered (UserPromptSubmit) or agent resumed (PostToolUse).
        TerminalHookEvents.UserPromptSubmit or TerminalHookEvents.PostToolUse => SpawnPhaseStatus(mode),
        _ => null,
    };

    private static string? SpawnPhaseStatus(string? mode) => mode switch
    {
        TerminalRunModes.Work => IntentStatusNames.Work,
        TerminalRunModes.Free => IntentStatusNames.Work,
        TerminalRunModes.Review => IntentStatusNames.Work,
        TerminalRunModes.Interview => IntentStatusNames.Interview,
        _ => null,
    };

    private static bool IsPhasedMode(string? mode) =>
        mode is TerminalRunModes.Work or TerminalRunModes.Interview or TerminalRunModes.Review or TerminalRunModes.Free;
}
