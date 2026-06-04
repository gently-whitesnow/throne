using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Terminals;

public sealed class RunPreflightCloneScheduler(
    IIntentRepositoryBindingRepository bindings,
    IRepositoryCloneRequests cloneQueue,
    RepositoryCloneTransitionWriter transitions)
{
    public async Task EnqueuePendingAndFailedAsync(IntentId intentId, CancellationToken ct)
    {
        var snapshot = await bindings.FindByIntentAsync(intentId, ct);
        foreach (var binding in snapshot)
        {
            if (binding.State.CloneStatus == CloneStatusNames.Pending)
            {
                await cloneQueue.EnqueueAsync(binding.Id, ct);
                continue;
            }

            if (binding.State.CloneStatus == CloneStatusNames.Failed)
            {
                _ = await transitions.MarkPendingRetryAsync(binding, ct);
                await cloneQueue.EnqueueAsync(binding.Id, ct);
            }
        }
    }
}
