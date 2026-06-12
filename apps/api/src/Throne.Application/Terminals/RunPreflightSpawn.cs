using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Domain.Intents;

namespace Throne.Application.Terminals;

/// <summary>
/// Workspace-path computation + tmux spawn invocation. Lives in its own type so the
/// orchestrator above stays within the project-wide CA1502 type-level budget.
/// </summary>
public sealed class RunPreflightSpawn(
    ITmuxSessionManager tmux,
    IWorkspaceRootProvider workspaceRoot,
    IWorkspaceTrust workspaceTrust,
    IClaudeSessionSettingsWriter claudeSettings,
    IDomainEventDispatcher events)
{
    public async Task SpawnAsync(
        IntentId intentId,
        string sessionName,
        string mode,
        TerminalLaunchOptions launch,
        CancellationToken ct)
    {
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intentId.Value);
        var prompt = AgentPromptBuilder.Build(mode, intentId.Value);
        var isFree = mode == TerminalRunModes.Free;

        // Trust the workspace before the agent boots in it, otherwise the CLI blocks on its
        // interactive trust prompt and the operator has to confirm by hand on every run. Which
        // trust store gets seeded depends on the launched vendor.
        await workspaceTrust.EnsureTrustedAsync(launch.Vendor, workspacePath, ct);

        // Free mode boots the agent bare and pre-types the prompt instead of passing it as argv —
        // an argv prompt auto-runs, but free mode hands an editable starter line to the operator.
        var settingsPath = launch.Vendor == TerminalAgentCatalog.VendorClaude
            ? await claudeSettings.WriteAsync(intentId.Value, workspacePath, ct)
            : null;
        var invocation = AgentSpawnCommand.Build(launch, prompt, isFree, settingsPath);
        var spawn = await tmux.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId: intentId.Value,
                WorkingDirectory: workspacePath,
                Command: invocation.Command,
                Arguments: invocation.Arguments),
            ct);

        if (!spawn.IsAlive)
        {
            throw TerminalFailures.SpawnFailed(intentId.Value, sessionName, spawn.Detail);
        }

        if (isFree)
        {
            await tmux.SendLiteralTextAsync(intentId.Value, prompt, ct);
        }

        await events.DispatchAsync(new TerminalSessionStarted(intentId.Value), ct);
    }

    public Task<bool> HasSessionAsync(string intentId, CancellationToken ct) =>
        tmux.HasSessionAsync(intentId, ct);

    public Task<bool> KillSessionAsync(string intentId, CancellationToken ct) =>
        tmux.KillSessionAsync(intentId, ct);
}
