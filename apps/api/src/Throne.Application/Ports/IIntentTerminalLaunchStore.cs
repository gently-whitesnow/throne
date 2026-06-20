using Throne.Application.Terminals;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence port for the per-intent terminal launch axis (mode/vendor/model/effort).
/// Keyed by intent id; at most one record per intent — there is at most one tmux session per
/// intent, so the same record serves both «live session's actual axis» and «last-used choice»
/// (ADR-0041). Liveness itself stays tmux-derived and is never stored here.
/// </summary>
public interface IIntentTerminalLaunchStore
{
    /// <summary>Read the persisted launch axis. Null when the intent was never launched.</summary>
    Task<TerminalLaunchRecord?> GetAsync(string intentId, CancellationToken ct);

    /// <summary>Upsert the launch axis for the intent. <paramref name="record"/> is pre-resolved.</summary>
    Task SaveAsync(string intentId, TerminalLaunchRecord record, CancellationToken ct);
}
