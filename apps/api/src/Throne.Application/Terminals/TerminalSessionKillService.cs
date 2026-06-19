using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

/// <summary>
/// Tears the per-intent tmux session down without respawning (the «kill session»
/// button). Unlike <see cref="RunPreflightOrchestrator"/> restart it never re-runs
/// pre-flight or spawns — it kills and reports the post-kill snapshot as exited.
/// </summary>
public sealed class TerminalSessionKillService(
    RunPreflightGuards guards,
    IIntentRepositoryBindingRepository bindings,
    IIntentTerminalLaunchStore launchStore,
    RunPreflightSpawn spawner)
{
    public async Task<RunPreflightResult> KillAsync(string intentId, CancellationToken ct)
    {
        await guards.EnsureCapabilityEnabledAsync(ct);
        var intent = await guards.LoadIntentAsync(intentId, ct);
        var sessionName = TmuxSessionName.For(intent.Id.Value);

        await spawner.KillSessionAsync(intent.Id.Value, ct);

        var snapshot = await bindings.FindByIntentAsync(intent.Id, ct);
        var launch = await launchStore.GetAsync(intent.Id.Value, ct);
        return RunPreflightSession.BuildResult(
            intent.Id.Value,
            sessionName,
            TerminalSessionStates.Exited,
            snapshot,
            RunPreflightSession.CollectBlocking(snapshot),
            launch);
    }
}
