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
    private const string UserPromptFileName = "throne-session.user-prompt.txt";

    // Bracketed-paste markers (-p on paste-buffer) only wrap the payload if the vendor TUI has
    // already negotiated bracketed-paste mode (DECSET ?2004h). Between `new-session` returning
    // and the TUI's terminfo init there is a small window where a paste-buffer would deliver \n
    // as Enter mid-prompt. claude/codex mount fast — empirically a quarter second is enough
    // headroom; if a particular box turns out to be slower, lift to TmuxOptions.
    private static readonly TimeSpan BracketedPasteWarmup = TimeSpan.FromMilliseconds(300);

    private readonly Dictionary<string, ISessionHookAdapter> _hookAdapters =
        hookAdapters.ToDictionary(a => a.Vendor, StringComparer.Ordinal);

    public async Task SpawnAsync(
        IntentId intentId,
        string sessionName,
        string mode,
        TerminalLaunchOptions launch,
        TerminalSpawnPrompt prompt,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intentId.Value);

        // Trust the workspace before the agent boots in it, otherwise the CLI blocks on its
        // interactive trust prompt and the operator has to confirm by hand on every run. Which
        // trust store gets seeded depends on the launched vendor.
        await workspaceTrust.EnsureTrustedAsync(launch.Vendor, workspacePath, ct);

        // Embedded contour injects the operator-curated rules/task upfront (ADR-0034) instead of a
        // hardcoded bundle prompt. Neither rides on the spawn argv — the rules block goes via the
        // vendor adapter's file-backed reference (Claude --append-system-prompt-file, Codex -p
        // profile), the user task is pasted into the live pane after spawn from a file. An empty
        // task skips the paste so the agent boots bare and the operator types it themselves.
        var preparedArgs = _hookAdapters.TryGetValue(launch.Vendor, out var adapter)
            ? await adapter.PrepareSpawnArgsAsync(intentId.Value, workspacePath, mode, prompt.SystemPrompt, ct)
            : [];
        var invocation = AgentSpawnCommand.Build(launch, preparedArgs);
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

        await DeliverUserPromptAsync(intentId.Value, workspacePath, prompt.UserPrompt, ct);

        await SetSpawnPhaseAsync(intentId.Value, mode, ct);

        await events.DispatchAsync(new TerminalSessionStarted(intentId.Value), ct);
    }

    private async Task DeliverUserPromptAsync(
        string intentId, string workspacePath, string? userPrompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        Directory.CreateDirectory(workspacePath);
        var promptPath = Path.Combine(workspacePath, UserPromptFileName);
        await File.WriteAllTextAsync(promptPath, userPrompt, ct);

        await Task.Delay(BracketedPasteWarmup, ct);
        await tmux.PasteFileAsSubmittedPromptAsync(intentId, promptPath, ct);
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
        TerminalRunModes.Free => IntentStatusNames.Work,
        TerminalRunModes.Interview => IntentStatusNames.Interview,
        _ => null,
    };
}
