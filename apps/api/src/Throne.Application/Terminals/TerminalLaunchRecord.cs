namespace Throne.Application.Terminals;

/// <summary>
/// Persisted launch axis of one intent: the resolved mode + vendor/model/effort the last
/// spawn actually used, plus the session skills hot-attached into the live session via
/// <c>POST /terminal/skills/attach</c> and the per-mode skill selection remembered for the
/// next preflight. Stored per intent and echoed back by the run response and the status
/// probe so the launch controls restore the operator's per-intent choice and, while a session
/// is live, show its real parameters (ADR-0041). Values are wire strings (snake_case
/// constants) — no enum serialization on the persistence boundary.
/// <para>
/// <see cref="AttachedSkillIds"/> tracks skills loaded into a running session without
/// respawn — a runtime indicator for the live-session badges. Empty when no hot-attach has
/// happened (or the previous tmux session has been torn down upstream).
/// </para>
/// <para>
/// <see cref="SelectedSkillIdsByMode"/> is the unified «remembered» source for the launch
/// modal: written on every successful spawn (the run pipeline persists the curated set for
/// the spawned mode) and merged on hot-attach for the current mode, so hot-attached skills
/// survive a respawn without a separate store. Empty map when the intent was never launched.
/// </para>
/// </summary>
public sealed record TerminalLaunchRecord(
    string Mode,
    string Vendor,
    string Model,
    string? Effort,
    IReadOnlyList<string> AttachedSkillIds,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SelectedSkillIdsByMode);
