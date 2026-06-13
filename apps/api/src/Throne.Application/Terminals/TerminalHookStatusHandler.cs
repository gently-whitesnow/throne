using Throne.Application.Intents;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Terminals;

/// <summary>
/// Derives intent status from an embedded-terminal hook callback (ADR-0034 §4). Replaces the
/// model-cooperative MCP path in the embedded contour: <c>Stop</c> parks the intent in
/// <c>awaiting_operator</c>, <c>UserPromptSubmit</c> returns it to the spawn phase. The mapping is
/// deterministic and stateless — the spawn phase rides in on the hook URL, no session-mode store.
/// Idempotent by construction (repeated set to the same status is a no-op at the domain level), so a
/// double transition via hook + bundle is harmless.
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
    /// without an intent and has no phase to park or return to. <c>free</c> is phased like work
    /// (spawn→work, Stop→awaiting_operator, UserPromptSubmit→work); only its context injection is
    /// bare (the operator curates everything), not its status lifecycle.
    /// </summary>
    private static string? ResolveTargetStatus(string hookEvent, string? mode) => hookEvent switch
    {
        TerminalHookEvents.Stop when IsPhasedMode(mode) => IntentStatusNames.AwaitingOperator,
        TerminalHookEvents.UserPromptSubmit when mode == TerminalRunModes.Work => IntentStatusNames.Work,
        TerminalHookEvents.UserPromptSubmit when mode == TerminalRunModes.Free => IntentStatusNames.Work,
        TerminalHookEvents.UserPromptSubmit when mode == TerminalRunModes.Interview => IntentStatusNames.Interview,
        _ => null,
    };

    private static bool IsPhasedMode(string? mode) =>
        mode is TerminalRunModes.Work or TerminalRunModes.Interview or TerminalRunModes.Free;
}
