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
    TerminalLaunchResolver launchResolver,
    IIntentTerminalLaunchStore launchStore)
{
    public async Task<RunPreflightResult> RunAsync(
        string intentId,
        string mode,
        TerminalLaunchInput launch,
        TerminalSpawnPrompt prompt,
        bool restart,
        CancellationToken ct,
        string? reviewBindingId = null)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(prompt);
        RunPreflightModeGuard.EnsureKnown(mode);
        var launchOptions = await launchResolver.ResolveAsync(launch.Vendor, launch.Model, launch.Effort, ct);
        var launchRecord = new TerminalLaunchRecord(
            mode, launchOptions.Vendor, launchOptions.Model, launchOptions.Effort);
        await guards.EnsureCapabilityEnabledAsync(ct);

        var intent = await guards.LoadIntentAsync(intentId, ct);
        var sessionName = TmuxSessionName.For(intent.Id.Value);
        await guards.EnsureSessionSlotAsync(intent.Id.Value, sessionName, restart, ct);

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
                launchRecord);
        }
        var reviewArtifact = ReviewArtifactWriteTarget.Resolve(mode, reviewBindingId, waitResult.Bindings);

        // Validate the curated selection and persist the task-zone edit (optimistic concurrency)
        // before spawn — a version conflict throws here so the agent never starts on a stale edit.
        await promptGate.ApplyAsync(intent, mode, prompt, ct);

        await spawner.SpawnAsync(intent.Id, sessionName, mode, launchOptions, prompt, reviewArtifact, ct);
        await launchStore.SaveAsync(intent.Id.Value, launchRecord, ct);
        return RunPreflightSession.BuildResult(
            intent.Id.Value, sessionName, TerminalSessionStates.Running, waitResult.Bindings, blockingBindings: [],
            launchRecord);
    }
}
