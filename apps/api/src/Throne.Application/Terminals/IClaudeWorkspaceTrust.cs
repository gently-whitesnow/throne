namespace Throne.Application.Terminals;

/// <summary>
/// Pre-seeds the agent CLI's per-directory trust so a freshly-created intent workspace
/// boots straight into work instead of stopping on the interactive "Is this a project you
/// trust?" prompt. Throne owns every workspace it spawns into (clones of the operator's own
/// repos under the workspace root), so trusting them a priori is safe and saves the operator
/// an Enter keypress on each run.
///
/// Implemented in Infrastructure — Application must not know the agent config file path,
/// home-dir expansion or JSON layout. Best-effort by contract: a failure to seed trust never
/// blocks the spawn (the operator just sees the prompt once, as before).
/// </summary>
public interface IClaudeWorkspaceTrust
{
    /// <summary>
    /// Marks <paramref name="workspacePath"/> (the absolute cwd the agent is launched in) as
    /// trusted. Idempotent and side-effect-free when the directory is already trusted.
    /// Never throws — implementations swallow and log I/O / parse failures.
    /// </summary>
    Task EnsureTrustedAsync(string workspacePath, CancellationToken ct);
}
