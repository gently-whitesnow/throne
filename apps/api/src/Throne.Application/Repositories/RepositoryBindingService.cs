using Microsoft.Extensions.Logging;
using Throne.Application.Errors;
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
public sealed partial class RepositoryBindingService(
    RepositoryBindingResolver resolver,
    RepositoryBindingPersistence persistence,
    RepositoryPullRequestSyncWorkflow syncWorkflow,
    RepositoryCloneTransitionWriter cloneWriter,
    IRepositoryCloneRequests cloneQueue,
    PullRequestAutoBindWorkflow autoBind,
    ILogger<RepositoryBindingService> logger
)
{
    /// <param name="enqueueClone">
    /// Schedule the clone right after persisting the binding. The standalone manual-bind
    /// endpoint passes <c>true</c> so the clone starts immediately. The Run pre-flight
    /// auto-bind passes <c>false</c>: <see cref="Terminals.RunPreflightCloneScheduler"/> runs
    /// in the very next orchestrator step and enqueues every <c>pending</c> binding, so a
    /// bind-time enqueue here would queue the same binding twice for one Run.
    /// </param>
    public async Task<IntentRepositoryBinding> BindAsync(
        BindRepositoryCommand command,
        CancellationToken ct,
        bool enqueueClone = true
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var intentId = await resolver.EnsureIntentExistsAsync(command.IntentId, ct);
        await resolver.EnsureProviderSurfaceEnabledAsync(command.Provider, ct);
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

    public async Task<bool> TryUnbindAsync(UnbindRepositoryCommand command, CancellationToken ct)
    {
        try
        {
            await UnbindAsync(command, ct);
            return true;
        }
        catch (ApiException ex)
            when (ex.Code is ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound)
        {
            return false;
        }
    }

    /// <summary>
    /// «Обновить» disk-recovery (ADR-0024) + on-demand PR auto-bind. Disk path: trigger is purely
    /// the on-disk folder — the Mongo <c>clone_status</c> is ignored. Folder missing → flip the
    /// binding back to <c>pending</c> (unless already queued) and re-enqueue the clone; the
    /// worker's <c>pending → cloning</c> CAS de-dupes a double enqueue. Realtime
    /// <c>IntentRepositoryCloneProgress</c> (raised by the transition writer) drives the UI to
    /// <c>ready</c>. Folder present → no clone work, but when the binding has no PR attached we
    /// run a single <see cref="PullRequestAutoBindWorkflow"/> pass for it so the user does not
    /// have to wait for the next <see cref="PullRequestSyncTickWorkflow"/> tick to see a
    /// freshly-opened PR.
    /// </summary>
    public async Task<IntentRepositoryBinding> RefreshAsync(
        RefreshRepositoryBindingCommand command,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var binding = await resolver.LoadBindingAsync(command.IntentId, command.BindingId, ct);
        if (persistence.LocalCloneExists(binding))
        {
            if (binding.State.PullRequestNumber is null
                && binding.State.CloneStatus == CloneStatusNames.Ready)
            {
                var report = await autoBind.RunForAsync(binding, ct);
                LogRefreshAutoBind(
                    logger, binding.Id.Value, report.Bound, report.Skipped, report.Failed);
            }
            else
            {
                LogRefreshNoop(
                    logger,
                    binding.Id.Value,
                    binding.State.CloneStatus,
                    binding.State.PullRequestNumber);
            }
            return binding;
        }

        if (binding.State.CloneStatus != CloneStatusNames.Pending)
        {
            var outcome = await cloneWriter.MarkPendingForRefreshAsync(binding, ct);
            if (!outcome.WasPersisted)
            {
                throw RepositoryBindingFailures.BindingNotFound(
                    command.IntentId,
                    command.BindingId
                );
            }
        }
        await cloneQueue.EnqueueAsync(binding.Id, ct);
        LogRefreshReclone(logger, binding.Id.Value, binding.State.CloneStatus);
        return binding;
    }

    public async Task<IReadOnlyList<IntentRepositoryBinding>> ListByIntentAsync(
        string intentId,
        CancellationToken ct
    )
    {
        var resolved = await resolver.EnsureIntentExistsAsync(intentId, ct);
        return await persistence.FindByIntentAsync(resolved, ct);
    }

    public async Task<IntentRepositoryBinding> AttachPullRequestAsync(
        AttachRepositoryPullRequestCommand command,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var binding = await resolver.LoadBindingAsync(command.IntentId, command.BindingId, ct);
        if (binding.State.PullRequestNumber is not null)
        {
            throw new ApiException(
                ErrorCodes.RepositoryPullRequestAlreadyAttached,
                $"Binding '{binding.Id.Value}' already tracks pull request #{binding.State.PullRequestNumber}. "
                    + "Unbind and rebind to switch the tracked pull request.",
                new Dictionary<string, object?>
                {
                    ["binding_id"] = binding.Id.Value,
                    ["pull_request_number"] = binding.State.PullRequestNumber,
                }
            );
        }

        return await persistence.AttachPullRequestAsync(binding, command.PullRequestNumber, ct);
    }

    public async Task<SyncRepositoryPullRequestResult> SyncPullRequestAsync(
        SyncRepositoryPullRequestCommand command,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(command);

        var binding = await resolver.LoadBindingAsync(command.IntentId, command.BindingId, ct);
        var provider = resolver.ResolveProvider(binding.Coordinate.Provider);
        return await syncWorkflow.RefreshAndSyncAsync(binding, provider, ct);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "RepositoryBindingService.Refresh: binding {BindingId} auto-bind pass bound={Bound}, skipped={Skipped}, failed={Failed}.")]
    private static partial void LogRefreshAutoBind(
        ILogger logger, string bindingId, int bound, int skipped, int failed);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "RepositoryBindingService.Refresh: binding {BindingId} no-op (clone_status={CloneStatus}, pull_request_number={Pr}).")]
    private static partial void LogRefreshNoop(
        ILogger logger, string bindingId, string cloneStatus, int? pr);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "RepositoryBindingService.Refresh: binding {BindingId} local clone missing (was {PrevCloneStatus}) — re-enqueued.")]
    private static partial void LogRefreshReclone(
        ILogger logger, string bindingId, string prevCloneStatus);
}
