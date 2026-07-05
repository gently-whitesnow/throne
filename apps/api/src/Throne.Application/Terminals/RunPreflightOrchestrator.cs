using Throne.Application.Ports;

namespace Throne.Application.Terminals;

/// <summary>
/// Slice 2 Run pre-flight pipeline (`POST /api/v1/intents/{id}/terminal/run`). Sequencer
/// around <see cref="RunPreflightGuards"/>, <see cref="RunPreflightAutoBind"/>,
/// <see cref="RunPreflightCloneWait"/> and <see cref="RunPreflightSpawn"/>. The per-stage
/// types own all branching logic so this orchestrator stays within the project-wide
/// CA1502 type-level cyclomatic budget.
/// </summary>
public sealed class RunPreflightOrchestrator(
    RunPreflightGuards guards,
    RunPreflightAutoBind autoBind,
    RunPreflightCloneScheduler cloneQueue,
    RunPreflightCloneWait cloneWait,
    RunPreflightSpawn spawner,
    RunPreflightPromptGate promptGate,
    RunPreflightSkillPlanner skills,
    RunPreflightLaunchPlanner launches)
{
    public Task<RunPreflightResult> RunAsync(
        string intentId,
        string mode,
        TerminalLaunchInput launch,
        TerminalSpawnPrompt prompt,
        CancellationToken ct,
        string? reviewBindingId = null) =>
        RunAsync(intentId, mode, launch, prompt, selectedSkillIds: null, ct, reviewBindingId);

    public async Task<RunPreflightResult> RunAsync(
        string intentId,
        string mode,
        TerminalLaunchInput launch,
        TerminalSpawnPrompt prompt,
        IReadOnlyList<string>? selectedSkillIds,
        CancellationToken ct,
        string? reviewBindingId = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        RunPreflightModeGuard.EnsureKnown(mode);
        var launchPlan = await launches.ResolveAsync(mode, launch, ct);
        await guards.EnsureTmuxDetectedAsync(ct);

        var intent = await guards.LoadIntentAsync(intentId, ct);
        var sessionName = TmuxSessionName.For(intent.Id.Value);
        await guards.EnsureSessionSlotAsync(intent.Id.Value, sessionName, ct);

        await autoBind.ApplyAsync(intent, ct);
        await cloneQueue.EnqueuePendingAndFailedAsync(intent.Id, ct);
        var waitResult = await cloneWait.WaitAsync(intent.Id, ct);
        RunPreflightSession.EnsureWaitDidNotTimeOut(intent.Id.Value, waitResult);

        var blocking = RunPreflightSession.CollectBlocking(waitResult.Bindings);
        if (blocking.Count > 0)
        {
            // Echo the attempted axis but do NOT persist it — nothing spawned, so it is not the
            // intent's last-used launch.
            return RunPreflightSession.BuildResult(
                intent.Id.Value, sessionName, TerminalSessionStates.Blocked, waitResult.Bindings, blocking,
                launchPlan.Record);
        }
        var skillPlan = skills.Build(
            intent.Id.Value, launchPlan.Options.Vendor, selectedSkillIds, reviewBindingId, waitResult.Bindings);

        // Validate the curated selection and persist the task-zone edit (optimistic concurrency)
        // before spawn — a version conflict throws here so the agent never starts on a stale edit.
        await promptGate.ApplyAsync(intent, mode, prompt, ct);

        await spawner.SpawnAsync(
            intent.Id,
            sessionName,
            mode,
            launchPlan.Options,
            prompt,
            skillPlan.Packages,
            skillPlan.SelectedSkillIds,
            RunPreflightSession.CollectReadyRepoPaths(waitResult.Bindings),
            intent.TagIds,
            intent.State.Title,
            ct);
        await launches.SaveAsync(intent.Id.Value, launchPlan, ct);
        await skills.SaveAsync(intent.Id.Value, mode, skillPlan, ct);
        return RunPreflightSession.BuildResult(
            intent.Id.Value, sessionName, TerminalSessionStates.Running, waitResult.Bindings, blockingBindings: [],
            launchPlan.Record);
    }
}
