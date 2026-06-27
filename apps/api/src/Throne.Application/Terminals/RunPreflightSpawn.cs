using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Intents;
using Throne.Domain.Intents;
using Throne.Domain.Intents.Training;
using Throne.Domain.Tags;

namespace Throne.Application.Terminals;

/// <summary>
/// Workspace-path computation + tmux spawn invocation. Lives in its own type so the
/// orchestrator above stays within the project-wide CA1502 type-level budget.
/// </summary>
public sealed class RunPreflightSpawn(
    ITmuxSessionManager tmux,
    IWorkspaceRootProvider workspaceRoot,
    RunPreflightWorkspacePreparer workspacePreparer,
    IEnumerable<ISessionHookAdapter> hookAdapters,
    IRunPreflightPromptDelivery promptDelivery,
    RunPreflightOptions options,
    ITerminalVendorCatalog vendors,
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
        TerminalSpawnPrompt prompt,
        IReadOnlyList<SessionSkillPackage> skillPackages,
        IReadOnlyList<string> repoPaths,
        IReadOnlyList<TagId> tagIds,
        TerminalViewport? viewport,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", intentId.Value);

        // Trust + reset stale per-run staging + dump attachments, before the adapter re-seeds skills
        // (PrepareSpawnArgsAsync) and the agent boots.
        await workspacePreparer.PrepareAsync(intentId, launch.Vendor, workspacePath, ct);

        // Embedded contour injects the operator-curated rules/task upfront (ADR-0034) instead of a
        // hardcoded bundle prompt. Neither rides on the spawn argv — the rules block goes via the
        // vendor adapter's file-backed reference (Claude --append-system-prompt-file, Codex -p
        // profile), the user task is pasted into the live pane after spawn from a file. An empty
        // task skips the paste so the agent boots bare and the operator types it themselves.
        _hookAdapters.TryGetValue(launch.Vendor, out var adapter);
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

        var descriptor = vendors.DescriptorFor(launch.Vendor);
        var invocation = AgentSpawnCommand.Build(descriptor, launch, preparedArgs);
        var spawn = await tmux.SpawnAsync(
            new TmuxSpawnRequest(
                IntentId: intentId.Value,
                WorkingDirectory: workspacePath,
                Command: invocation.Command,
                Arguments: invocation.Arguments,
                EnableMouse: descriptor.EnableMouse,
                EnvironmentVariables: BuildSessionEnvironment(intentId.Value, skillPackages),
                Cols: viewport?.Cols,
                Rows: viewport?.Rows),
            ct);

        if (!spawn.IsAlive)
        {
            throw TerminalFailures.SpawnFailed(intentId.Value, sessionName, spawn.Detail);
        }

        await SetSpawnPhaseAsync(intentId.Value, mode, ct);
        await events.DispatchAsync(new TerminalSessionStarted(intentId.Value), ct);

        // The session is alive — return Running now so the front attaches the live pane immediately.
        // Native-session vendors (OpenCode) already delivered the prompt before spawn; everyone else
        // gets the readiness-gated paste + submit confirmation as a detached best-effort task so a
        // slow TUI or absorbed Enter never fails the spawn — it surfaces as a soft inline hint.
        if (adapter is not INativeSessionInitializer && !string.IsNullOrWhiteSpace(prompt.UserPrompt))
        {
            promptDelivery.Kick(new TerminalPromptDeliveryRequest(
                intentId.Value, mode, launch.Vendor, adapter, workspacePath, repoPaths, tagIds, prompt.UserPrompt));
        }
    }

    private Dictionary<string, string> BuildSessionEnvironment(
        string intentId,
        IReadOnlyList<SessionSkillPackage> skillPackages)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["THRONE_INTENT_ID"] = intentId,
            ["THRONE_API_BASE"] = NormalizeApiBaseUrl(options.ApiBaseUrl),
        };

        foreach (var package in skillPackages)
        {
            if (package is ReviewSessionSkillPackage review)
            {
                env["THRONE_REPOSITORY_BINDING_ID"] = review.Target.BindingId;
                break;
            }
        }

        return env;
    }

    private static string NormalizeApiBaseUrl(string? apiBaseUrl) =>
        string.IsNullOrWhiteSpace(apiBaseUrl)
            ? SessionHookOptions.DefaultApiBaseUrl
            : apiBaseUrl.TrimEnd('/');

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
