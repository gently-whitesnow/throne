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
    SessionSkillPackageRegistry skillPackageRegistry,
    TmuxTuiReadinessWaiter readinessWaiter,
    RunPreflightOptions options,
    SetIntentStatusHandler setStatus,
    IDomainEventDispatcher events)
{
    private const string SourcePrefix = "terminal:spawn:";
    private const string UserPromptFileName = "throne-session.user-prompt.txt";

    private readonly Dictionary<string, ISessionHookAdapter> _hookAdapters =
        hookAdapters.ToDictionary(a => a.Vendor, StringComparer.Ordinal);

    public async Task SpawnAsync(
        IntentId intentId,
        string sessionName,
        string mode,
        TerminalLaunchOptions launch,
        TerminalSpawnPrompt prompt,
        ReviewArtifactWriteTarget? reviewArtifact,
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
        _hookAdapters.TryGetValue(launch.Vendor, out var adapter);
        var skillPackages = skillPackageRegistry.Resolve(
            new SessionSkillPackageResolution(intentId.Value, mode, launch.Vendor, reviewArtifact));
        IReadOnlyList<string> preparedArgs = adapter is not null
            ? await adapter.PrepareSpawnArgsAsync(
                intentId.Value, workspacePath, mode, prompt.SystemPrompt, skillPackages, ct)
            : [];

        // Native-session vendors (OpenCode) own their prompt delivery *before* the visible pane
        // spawns: the loop runs in a shared `opencode serve`, the pane only attaches. Create the
        // session + submit the prompt here and fold the returned attach argv into the spawn, so the
        // front pulls the right session by id — no post-spawn command-bus push to race (this
        // replaces the old best-effort select-session focus).
        if (adapter is INativeSessionInitializer initializer)
        {
            var attachArgs = await InitializeNativeSessionAsync(
                initializer, intentId.Value, launch.Vendor, launch.Model, workspacePath, prompt.UserPrompt, ct);
            preparedArgs = [.. preparedArgs, .. attachArgs];
        }

        var invocation = AgentSpawnCommand.Build(launch, preparedArgs);
        using var readiness = adapter is not null
            ? readinessWaiter.Arm(intentId.Value, adapter)
            : null;
        var spawn = await tmux.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId: intentId.Value,
                WorkingDirectory: workspacePath,
                Command: invocation.Command,
                Arguments: invocation.Arguments,
                EnableMouse: string.Equals(launch.Vendor, TerminalAgentCatalog.VendorOpencode, StringComparison.Ordinal)),
            ct);

        if (!spawn.IsAlive)
        {
            throw TerminalFailures.SpawnFailed(intentId.Value, sessionName, spawn.Detail);
        }

        // Native-session vendors already delivered the prompt before spawn (see above); the pane
        // only attaches. Everyone else pastes the task into the live pane after a readiness gate.
        if (adapter is not INativeSessionInitializer)
        {
            await DeliverUserPromptAsync(
                intentId.Value, launch.Vendor, adapter, readiness, workspacePath, prompt.UserPrompt, ct);
        }

        await SetSpawnPhaseAsync(intentId.Value, mode, ct);

        await events.DispatchAsync(new TerminalSessionStarted(intentId.Value), ct);
    }

    private async Task DeliverUserPromptAsync(
        string intentId,
        string vendor,
        ISessionHookAdapter? adapter,
        TerminalReadinessSignals.TerminalReadinessRegistration? readiness,
        string workspacePath,
        string? userPrompt,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userPrompt))
        {
            return;
        }

        Directory.CreateDirectory(workspacePath);
        var promptPath = Path.Combine(workspacePath, UserPromptFileName);
        await File.WriteAllTextAsync(promptPath, userPrompt, ct);

        // Vendor TUI readiness gate (ADR-0026 follow-up): a blind warmup raced spawn → paste
        // and silently dropped the user prompt whenever the TUI took longer to init than the
        // sleep. Only vendors we have an adapter for get the gate — unknown vendors fall through
        // and skip the wait rather than block forever on a probe that has no marker.
        if (adapter is not null)
        {
            var result = await readinessWaiter.WaitAsync(intentId, adapter, readiness, ct);
            if (!result.IsReady)
            {
                throw TerminalFailures.TuiReadinessTimeout(
                    intentId,
                    vendor,
                    options.TuiReadinessTimeoutMilliseconds,
                    result.Attempts,
                    result.LastSnapshot);
            }
        }

        await tmux.PasteFileAsSubmittedPromptAsync(intentId, promptPath, ct);
    }

    private static async Task<IReadOnlyList<string>> InitializeNativeSessionAsync(
        INativeSessionInitializer initializer,
        string intentId,
        string vendor,
        string model,
        string workspacePath,
        string? userPrompt,
        CancellationToken ct)
    {
        try
        {
            return await initializer.InitializeSessionAsync(intentId, workspacePath, model, userPrompt, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw TerminalFailures.InitialPromptSubmitFailed(intentId, vendor, ex.Message);
        }
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
        TerminalRunModes.Review => IntentStatusNames.Work,
        TerminalRunModes.Interview => IntentStatusNames.Interview,
        _ => null,
    };
}
