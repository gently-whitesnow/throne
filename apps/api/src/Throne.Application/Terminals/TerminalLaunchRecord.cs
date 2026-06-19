namespace Throne.Application.Terminals;

/// <summary>
/// Persisted launch axis of one intent: the resolved mode + vendor/model/effort the last
/// spawn actually used. Stored per intent and echoed back by the run/restart response and the
/// status probe so the launch controls restore the operator's per-intent choice and, while a
/// session is live, show its real parameters (ADR-0041). All values are wire strings
/// (snake_case constants) — no enum serialization on the persistence boundary.
/// </summary>
public sealed record TerminalLaunchRecord(string Mode, string Vendor, string Model, string? Effort);
