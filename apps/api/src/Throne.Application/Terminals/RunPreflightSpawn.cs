using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;

namespace Throne.Application.Terminals;

/// <summary>
/// Workspace-path computation + tmux spawn invocation. Lives in its own type so the
/// orchestrator above stays within the project-wide CA1502 type-level budget.
/// </summary>
public sealed class RunPreflightSpawn(
    ITmuxSessionManager tmux,
    IWorkspaceRootProvider workspaceRoot,
    IWorkspaceTrust workspaceTrust,
    IEnumerable<ISessionHookAdapter> hookAdapters,
    SetIntentStatusHandler setStatus,
    IDomainEventDispatcher events)
{
    private const string SourcePrefix = "terminal:spawn:";

    private readonly Dictionary<string, ISessionHookAdapter> _hookAdapters =
        hookAdapters.ToDictionary(a => a.Vendor, StringComparer.Ordinal);

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
        var hookArgs = _hookAdapters.TryGetValue(launch.Vendor, out var adapter)
            ? await adapter.PrepareSpawnArgsAsync(intentId.Value, workspacePath, mode, ct)
            : [];
        var invocation = AgentSpawnCommand.Build(launch, prompt, isFree, hookArgs);
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

        await SetSpawnPhaseAsync(intentId.Value, mode, ct);

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

    private async Task SetSpawnPhaseAsync(string intentId, string mode, CancellationToken ct)
    {
        var status = SpawnPhaseStatus(mode);
        if (status is null)
        {
            return;
        }

        await setStatus.HandleAsync(
            new SetIntentStatusCommand(
                intentId,
                status,
                Reason: null,
                IntentTrainingAuthor.System,
                SourcePrefix + mode),
            ct);
    }

    private static string? SpawnPhaseStatus(string mode) => mode switch
    {
        TerminalRunModes.Work => IntentStatusNames.Work,
        TerminalRunModes.Interview => IntentStatusNames.Interview,
        _ => null,
    };
}
