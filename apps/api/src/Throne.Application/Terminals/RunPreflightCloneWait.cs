using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

/// <summary>
/// Blocking poll loop used by the pre-flight pipeline: waits until every binding of the
/// intent reaches a terminal <c>clone_status</c> (<c>ready</c> | <c>failed</c> |
/// <c>broken</c>). Returns the final snapshot so the caller can decide whether to spawn.
/// </summary>
public sealed class RunPreflightCloneWait(
    IIntentRepositoryBindingRepository bindings,
    RunPreflightOptions options,
    TimeProvider clock)
{
    public async Task<RunPreflightWaitResult> WaitAsync(IntentId intentId, CancellationToken ct)
    {
        var deadlineSeconds = Math.Max(1, options.CloneWaitSeconds);
        var pollDelay = TimeSpan.FromMilliseconds(Math.Max(50, options.PollIntervalMilliseconds));
        var deadline = clock.GetUtcNow().AddSeconds(deadlineSeconds);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var snapshot = await bindings.FindByIntentAsync(intentId, ct);
            if (snapshot.Count == 0 || AllTerminal(snapshot))
            {
                return new RunPreflightWaitResult(
                    Bindings: snapshot,
                    TimedOut: false,
                    WaitedSeconds: 0);
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return new RunPreflightWaitResult(
                    Bindings: snapshot,
                    TimedOut: true,
                    WaitedSeconds: deadlineSeconds);
            }

            await Task.Delay(pollDelay, clock, ct);
        }
    }

    private static bool AllTerminal(IReadOnlyList<IntentRepositoryBinding> snapshot)
    {
        foreach (var binding in snapshot)
        {
            if (!IsTerminal(binding.State.CloneStatus))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsTerminal(string cloneStatus) =>
        cloneStatus is CloneStatusNames.Ready or CloneStatusNames.Failed or CloneStatusNames.Broken;
}

/// <summary>
/// Outcome of one <see cref="RunPreflightCloneWait.WaitAsync"/> pass.
/// </summary>
public sealed record RunPreflightWaitResult(
    IReadOnlyList<IntentRepositoryBinding> Bindings,
    bool TimedOut,
    int WaitedSeconds);
