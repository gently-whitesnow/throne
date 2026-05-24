using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Application use-cases for the intent ↔ repository binding aggregate (ADR-0024,
/// parent slice <c>0fad9876661c450ebf89b86b9335516c</c>, T-08). Drives the HTTP module
/// (T-11) and is intentionally NOT exposed via MCP write-surface in slice 1
/// (ADR-0024 § 8).
///
/// Responsibilities:
/// <list type="bullet">
///   <item><c>BindAsync</c> — auth-precondition through <see cref="IGitProvider.GetAuthStatusAsync"/>,
///         workspace-path computation per ADR-0024 § 1, persistence (with the
///         <c>(intent_id, provider, owner, repo)</c> uniqueness invariant), and enqueue
///         into the background clone queue (T-09).</item>
///   <item><c>UnbindAsync</c> — drop the binding; the workspace directory is left on
///         disk (slice 6 owns cleanup).</item>
///   <item><c>ListByIntentAsync</c> — read projection consumed by the HTTP listing
///         endpoint and MCP <c>get_intent.repositories</c> (T-13).</item>
///   <item><c>SyncPullRequestAsync</c> — synchronous manual PR-comment refresh
///         (parent Q5 / ADR-0024 § 6); per-comment fanout is owned by T-10.</item>
/// </list>
///
/// Realtime emission goes through the dispatching unit-of-work
/// (<see cref="DomainEventDispatchingUnitOfWork"/>): outcome carriers raise
/// <see cref="IntentRepositoryBound"/> / <see cref="IntentRepositoryUnbound"/> /
/// <see cref="RepositoryPullRequestSynced"/> and T-12 emitters subscribe.
/// Sub-workflows (resolve / persist / sync) live in dedicated helpers so this type
/// stays inside the per-class CA1502 cyclomatic budget and within the ctor-deps
/// budget enforced by the maintainability gate.
/// </summary>
public sealed class RepositoryBindingService(
    RepositoryBindingResolver resolver,
    RepositoryBindingPersistence persistence,
    RepositoryPullRequestSyncWorkflow syncWorkflow,
    IRepositoryCloneRequests cloneQueue,
    IWorkspaceRootProvider workspace)
{
    public async Task<IntentRepositoryBinding> BindAsync(BindRepositoryCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var intentId = await resolver.EnsureIntentExistsAsync(command.IntentId, ct);
        var provider = resolver.ResolveProvider(command.Provider);
        await RepositoryBindingResolver.EnsureProviderAuthenticatedAsync(provider, ct);

        var binding = persistence.BuildPendingBinding(command, intentId, workspace.ResolvedRoot);
        var created = await persistence.CreateAsync(binding, ct);
        await cloneQueue.EnqueueAsync(created.Id, ct);
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
