namespace Throne.Application.Terminals;

/// <summary>
/// Persisted launch axis of one intent: the resolved mode + vendor/model/effort the last
/// spawn actually used, plus the session skills hot-attached into the live session via
/// <c>POST /terminal/skills/attach</c>. Stored per intent and echoed back by the run/restart
/// response and the status probe so the launch controls restore the operator's per-intent
/// choice and, while a session is live, show its real parameters (ADR-0041). Values are wire
/// strings (snake_case constants) — no enum serialization on the persistence boundary.
/// <para>
/// <see cref="AttachedSkillIds"/> tracks skills loaded into a running session without
/// restart. The run/restart pipeline never overwrites it — only the attach handler updates
/// it through <see cref="Ports.IIntentTerminalLaunchStore.SetAttachedSkillIdsAsync"/>. Empty
/// when no hot-attach has happened (or the previous tmux session has been torn down upstream).
/// </para>
/// </summary>
public sealed record TerminalLaunchRecord(
    string Mode,
    string Vendor,
    string Model,
    string? Effort,
    IReadOnlyList<string> AttachedSkillIds);
