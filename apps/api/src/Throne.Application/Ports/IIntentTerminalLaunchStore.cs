using Throne.Application.Terminals;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for the per-intent terminal launch axis (mode/vendor/model/effort) and
/// the session-skills hot-attached to a live session. Keyed by intent id; at most one record
/// per intent — there is at most one tmux session per intent, so the same record serves both
/// «live session's actual axis» and «last-used choice» (ADR-0041). Liveness itself stays
/// tmux-derived and is never stored here.
/// </summary>
public interface IIntentTerminalLaunchStore
{
    /// <summary>Read the persisted launch axis. Null when the intent was never launched.</summary>
    Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct);

    /// <summary>
    /// Upsert the launch axis for the intent (mode/vendor/model/effort only).
    /// <see cref="TerminalLaunchRecord.AttachedSkillIds"/> is intentionally NOT touched —
    /// the run pipeline never overwrites hot-attached skills, they survive until
    /// explicit clearing via <see cref="SetAttachedSkillIdsAsync"/>.
    /// </summary>
    Task SaveAsync(string intentId, TerminalLaunchRecord record, CancellationToken ct);

    /// <summary>
    /// Replace the persisted <c>attached_skill_ids</c> for the intent without touching
    /// mode/vendor/model/effort. Targeted <c>$set</c> with <c>upsert=false</c>: when no
    /// launch record exists the call is a no-op (a session must have been spawned before
    /// attach is meaningful, so the document is always present for legitimate callers).
    /// Passing an empty list unsets the field.
    /// </summary>
    Task SetAttachedSkillIdsAsync(
        string intentId,
        IReadOnlyList<string> attachedSkillIds,
        CancellationToken ct);
}
