using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// MCP write-surface is intentionally NOT exposed for this aggregate (ADR-0024 § 8).
/// Unbind ("delete repository") removes the binding record AND its on-disk workspace
/// directory (the directory delete lives in <see cref="RepositoryBindingPersistence"/>).
/// Realtime emission flows through the dispatching unit-of-work
/// (<see cref="DomainEventDispatchingUnitOfWork"/>): outcome carriers raise
/// <see cref="IntentRepositoryBound"/> / <see cref="IntentRepositoryUnbound"/> /
/// <see cref="RepositoryPullRequestSynced"/>.
/// </summary>
public sealed class RepositoryBindingService(
    RepositoryBindingResolver resolver,
    RepositoryBindingPersistence persistence,
    RepositoryPullRequestSyncWorkflow syncWorkflow,
    IRepositoryCloneRequests cloneQueue)
{
    /// <param name="enqueueClone">
    /// Schedule the clone right after persisting the binding. The standalone manual-bind
    /// endpoint passes <c>true</c> so the clone starts immediately. The Run pre-flight
    /// auto-bind passes <c>false</c>: <see cref="Terminals.RunPreflightCloneScheduler"/> runs
    /// in the very next orchestrator step and enqueues every <c>pending</c> binding, so a
    /// bind-time enqueue here would queue the same binding twice for one Run.
    /// </param>
    public async Task<IntentRepositoryBinding> BindAsync(
        BindRepositoryCommand command, CancellationToken ct, bool enqueueClone = true)
    {
        ArgumentNullException.ThrowIfNull(command);

        var intentId = await resolver.EnsureIntentExistsAsync(command.IntentId, ct);
        var provider = resolver.ResolveProvider(command.Provider);
        await RepositoryBindingResolver.EnsureProviderAuthenticatedAsync(provider, ct);

        var binding = persistence.BuildPendingBinding(command, intentId);
        var created = await persistence.CreateAsync(binding, ct);
        if (enqueueClone)
        {
            await cloneQueue.EnqueueAsync(created.Id, ct);
        }
        return created;
    }

    public async Task UnbindAsync(UnbindRepositoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var binding = await resolver.LoadBindingAsync(command.IntentId, command.BindingId, ct);
        await persistence.DeleteAsync(binding, ct);
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> ListByIntentAsync(
        string intentId,
        CancellationToken ct)
    {
        var resolved = await resolver.EnsureIntentExistsAsync(intentId, ct);
        return await persistence.FindByIntentAsync(resolved, ct);
    }

    public async Task<SyncRepositoryPullRequestResult> SyncPullRequestAsync(
        SyncRepositoryPullRequestCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var binding = await resolver.LoadBindingAsync(command.IntentId, command.BindingId, ct);
        var provider = resolver.ResolveProvider(binding.Coordinate.Provider);
        return await syncWorkflow.RefreshAndSyncAsync(binding, provider, ct);
    }
}
