using Throne.Application.Terminals;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for the per-intent terminal launch axis (mode/vendor/model/effort) and the
/// per-mode skill selection the next preflight defaults to. Keyed by intent id; at most one
/// record per intent — there is at most one tmux session per intent, so the same record serves
/// both «live session's actual axis» and «last-used choice» (ADR-0041). Liveness itself stays
/// tmux-derived and is never stored here.
/// </summary>
public interface IIntentTerminalLaunchStore
{
    /// <summary>Read the persisted launch axis. Null when the intent was never launched.</summary>
    Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct);

    /// <summary>
    /// Upsert the launch axis for the intent (mode/vendor/model/effort only).
    /// <see cref="TerminalLaunchRecord.SelectedSkillIdsByMode"/> is intentionally NOT touched
    /// — the per-mode selection survives a respawn until explicitly updated via
    /// <see cref="SaveSelectedSkillIdsAsync"/>.
    /// </summary>
    Task SaveAsync(string intentId, TerminalLaunchRecord record, CancellationToken ct);

    /// <summary>
    /// Replace the persisted per-mode skill selection for the given mode. Called by the run
    /// pipeline after a successful spawn (the curated spawn selection) and by hot-attach (the
    /// union of the current selection with the newly attached skills), so the next preflight in
    /// this mode pre-fills with the effective set and the live-session badges reflect it. Other
    /// modes' entries are preserved.
    /// </summary>
    Task SaveSelectedSkillIdsAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> selectedSkillIds,
        CancellationToken ct);
}
