using Microsoft.Extensions.Logging;
using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Seeds per-directory trust in the agent CLI's <c>~/.claude.json</c> so spawning into a new
/// intent workspace skips the interactive trust prompt. Read-modify-write of the global
/// config file; the merge itself lives in <see cref="ClaudeTrustDocument"/>.
/// </summary>
internal sealed class ClaudeWorkspaceTrust(ILogger<ClaudeWorkspaceTrust> log) : IClaudeWorkspaceTrust
{
    // The agent CLI persists per-project state (incl. the accepted-trust flag) here.
    private static readonly string ConfigPath =
        WorkspacePathExpansion.ExpandHome("~/.claude.json");

    public Task EnsureTrustedAsync(string workspacePath, CancellationToken ct)
    {
        try
        {
            Seed(workspacePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a write failure just means the operator confirms the prompt once,
            // exactly as before. Never let it abort the spawn.
            TerminalsLog.ClaudeTrustSeedFailed(log, workspacePath, ex.Message);
        }

        return Task.CompletedTask;
    }

    private static void Seed(string workspacePath)
    {
        var existing = File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : null;

        var updated = ClaudeTrustDocument.WithTrustedWorkspace(existing, workspacePath);
        if (updated is null)
        {
            return;
        }

        AtomicWrite(updated);
    }

    // Write to a sibling temp file then atomically replace, so a crash mid-write cannot leave
    // the operator's global config truncated. A concurrently-exiting agent instance writing the
    // same file is an inherent race the CLI already runs against itself (last-writer-wins).
    private static void AtomicWrite(string content)
    {
        var tempPath = ConfigPath + ".throne-" + Environment.ProcessId + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, ConfigPath, overwrite: true);
    }
}
