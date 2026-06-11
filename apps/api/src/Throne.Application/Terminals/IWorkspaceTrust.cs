namespace Throne.Application.Terminals;

/// <summary>
/// Pre-seeds an agent CLI's per-directory trust so a freshly-created intent workspace boots
/// straight into work instead of stopping on the interactive "do you trust this folder?"
/// prompt. Throne owns every workspace it spawns into (clones of the operator's own repos
/// under the workspace root), so trusting them a priori is safe and lets agents run
/// autonomously / in the background.
///
/// Provider-neutral: the concrete trust store (file path, format, merge semantics) differs per
/// vendor and lives in Infrastructure — Application only names the vendor. Best-effort by
/// contract: a failure to seed trust never blocks the spawn (the operator just sees the prompt
/// once, as before), and an unknown vendor is a no-op.
/// </summary>
public interface IWorkspaceTrust
{
    /// <summary>
    /// Marks <paramref name="workspacePath"/> (the absolute cwd the agent is launched in) as
    /// trusted for <paramref name="vendor"/>. Idempotent and side-effect-free when the directory
    /// is already trusted. Never throws — implementations swallow and log I/O / parse failures.
    /// </summary>
    Task EnsureTrustedAsync(string vendor, string workspacePath, CancellationToken ct);
}
