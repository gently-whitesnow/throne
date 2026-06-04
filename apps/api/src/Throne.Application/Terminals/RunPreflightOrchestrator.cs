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
    RunPreflightSpawn spawner)
{
    public async Task<RunPreflightResult> RunAsync(
        string intentId,
        string mode,
        bool restart,
        CancellationToken ct)
    {
        RunPreflightModeGuard.EnsureKnown(mode);
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
            return RunPreflightSession.BuildResult(
                intent.Id.Value, sessionName, TerminalSessionStates.Blocked, waitResult.Bindings, blocking);
        }

        await spawner.SpawnAsync(intent.Id, sessionName, mode, ct);
        return RunPreflightSession.BuildResult(
            intent.Id.Value, sessionName, TerminalSessionStates.Running, waitResult.Bindings, blockingBindings: []);
    }
}
