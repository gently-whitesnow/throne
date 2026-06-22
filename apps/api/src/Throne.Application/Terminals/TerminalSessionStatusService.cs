using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed class TerminalSessionStatusService(
    RunPreflightGuards guards,
    IIntentRepositoryBindingRepository bindings,
    IIntentTerminalLaunchStore launchStore,
    ITmuxSessionManager tmux)
{
    public async Task<RunPreflightResult> GetAsync(string intentId, CancellationToken ct)
    {
        await guards.EnsureTmuxDetectedAsync(ct);
        var intent = await guards.LoadIntentAsync(intentId, ct);
        var sessionName = TmuxSessionName.For(intent.Id.Value);
        var snapshot = await bindings.FindByIntentAsync(intent.Id, ct);
        var launch = await launchStore.GetAsync(intent.Id.Value, ct);
        var state = await tmux.HasSessionAsync(intent.Id.Value, ct)
            ? TerminalSessionStates.Running
            : TerminalSessionStates.Exited;

        return RunPreflightSession.BuildResult(
            intent.Id.Value,
            sessionName,
            state,
            snapshot,
            RunPreflightSession.CollectBlocking(snapshot),
            launch);
    }
}
