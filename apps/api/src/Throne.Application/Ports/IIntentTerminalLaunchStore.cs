using Throne.Application.Terminals;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for the per-intent terminal launch axis (mode/vendor/model/effort), the
/// per-mode skill selection the next preflight defaults to, and the session-skills hot-attached
/// to a live session. Keyed by intent id; at most one record per intent — there is at most one
/// tmux session per intent, so the same record serves both «live session's actual axis» and
/// «last-used choice» (ADR-0041). Liveness itself stays tmux-derived and is never stored here.
/// </summary>
public interface IIntentTerminalLaunchStore
{
    /// <summary>Read the persisted launch axis. Null when the intent was never launched.</summary>
    Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct);

    /// <summary>
    /// Upsert the launch axis for the intent (mode/vendor/model/effort only).
    /// <see cref="TerminalLaunchRecord.AttachedSkillIds"/> and
    /// <see cref="TerminalLaunchRecord.SelectedSkillIdsByMode"/> are intentionally NOT touched
    /// — hot-attached skills and per-mode selection survive a respawn until explicitly updated
    /// via the dedicated methods.
    /// </summary>
    Task SaveAsync(string intentId, TerminalLaunchRecord record, CancellationToken ct);

    /// <summary>
    /// Replace the persisted per-mode skill selection for the given mode. Called by the run
    /// pipeline after a successful spawn so the next preflight in this mode pre-fills with the
    /// curated selection. Other modes' entries are preserved.
    /// </summary>
    Task SaveSelectedSkillIdsAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> selectedSkillIds,
        CancellationToken ct);

    /// <summary>
    /// Replace the persisted <c>attached_skill_ids</c> for the intent and union the same set
    /// into <c>selected_skill_ids_by_mode[mode]</c>, so hot-attached skills survive a respawn
    /// as default-on selections for the same mode. <paramref name="mode"/> is the spawn mode of
    /// the live session being mutated (the same value the next preflight will look up).
    /// Targeted <c>$set</c> with <c>upsert=false</c>: when no launch record exists the call is
    /// a no-op (a session must have been spawned before attach is meaningful). Passing an empty
    /// list unsets the <c>attached_skill_ids</c> field and leaves the per-mode selection alone
    /// — the operator has not changed their next-preflight intent, they have only torn down
    /// the live indicator.
    /// </summary>
    Task SetAttachedSkillIdsAsync(
        string intentId,
        string mode,
        IReadOnlyList<string> attachedSkillIds,
        CancellationToken ct);
}
