using Throne.Application.Git;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Workspace-path computation + tmux spawn invocation. Lives in its own type so the
/// orchestrator above stays within the project-wide CA1502 type-level budget.
/// </summary>
public sealed class RunPreflightSpawn(
    ITmuxSessionManager tmux,
    IWorkspaceRootProvider workspaceRoot)
{
    private const string AgentCommand = "claude";

    public async Task SpawnAsync(IntentId intentId, string sessionName, string mode, CancellationToken ct)
    {
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intentId.Value);
        var prompt = AgentPromptBuilder.Build(mode, intentId.Value);
        var spawn = await tmux.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId: intentId.Value,
                WorkingDirectory: workspacePath,
                Command: AgentCommand,
                Arguments: [prompt]),
            ct);

        if (!spawn.IsAlive)
        {
            throw TerminalFailures.SpawnFailed(intentId.Value, sessionName, spawn.Detail);
        }
    }

    public Task<bool> HasSessionAsync(string intentId, CancellationToken ct) =>
        tmux.HasSessionAsync(intentId, ct);

    public Task<bool> KillSessionAsync(string intentId, CancellationToken ct) =>
        tmux.KillSessionAsync(intentId, ct);
}
