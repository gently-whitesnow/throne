using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Per-binding clone orchestration consumed by the background
/// <c>RepositoryCloneService</c>. Splitting the clone choreography out of
/// the worker keeps the worker focused on the queue loop (cancellation, batching,
/// logging) and lets us unit-test the actual state-machine transitions without
/// spinning up a <see cref="Microsoft.Extensions.Hosting.BackgroundService"/>.
///
/// The workflow honours ADR-0024 § 5:
/// <list type="bullet">
///   <item><see cref="RunAsync"/>: <c>pending → cloning</c> (save+event),
///         <see cref="IGitProvider.CloneRepositoryAsync"/>,
///         <c>cloning → ready</c> on success / <c>cloning → failed</c> on exception
///         (save+event in both cases). Bindings that were already past <c>pending</c>
///         on entry are skipped — a duplicate enqueue is a no-op.</item>
///   <item>Every save is delegated to <see cref="RepositoryCloneTransitionWriter"/>
///         which wraps the save in <see cref="IUnitOfWork.ExecuteAsync"/> and
///         returns a <see cref="RepositoryCloneTransitionOutcome"/>, so the
///         dispatching unit-of-work fans <see cref="IntentRepositoryCloneProgress"/>
///         out without the worker touching realtime emitters directly.</item>
/// </list>
///
/// Outcomes are typed (<see cref="RunAsync"/> returns <see cref="CloneRunResult"/>)
/// so callers can log / react without re-reading the binding from Mongo.
/// </summary>
public sealed class RepositoryCloneWorkflow(
    IIntentRepositoryBindingRepository bindings,
    IGitProviderRegistry providers,
    RepositoryCloneTransitionWriter writer)
{
    public async Task<CloneRunResult> RunAsync(BindingId bindingId, CancellationToken ct)
    {
        var binding = await bindings.GetByIdAsync(bindingId, ct);
        if (binding is null)
        {
            return CloneRunResult.NotFound;
        }
        if (binding.State.CloneStatus != CloneStatusNames.Pending)
        {
            return CloneRunResult.AlreadyProcessed;
        }

        var provider = providers.GetByName(binding.Coordinate.Provider);
        if (provider is null)
        {
            await writer.MarkFailedAsync(binding, $"provider '{binding.Coordinate.Provider}' is not registered", ct);
            return CloneRunResult.ProviderMissing;
        }

        var claim = await writer.MarkCloningAsync(binding, ct);
        if (!claim.WasPersisted)
        {
            // Lost the pending → cloning CAS to a concurrent worker (the same binding is
            // enqueued twice per Run pre-flight) or the binding vanished. The winner owns
            // the `gh clone`, so skipping here is what prevents the second clone from
            // failing with «destination path already exists».
            return CloneRunResult.AlreadyProcessed;
        }
        return await ExecuteCloneAsync(binding, provider, ct);
    }

    /// <summary>
    /// Recovery transition for the startup pass: flips a single <c>cloning</c>
    /// binding to <c>failed(reason)</c> and emits a final progress event. Called
    /// per binding by <see cref="RepositoryCloneRecoveryWorkflow"/>.
    /// </summary>
    public Task<RepositoryCloneTransitionOutcome> MarkInterruptedAsync(
        IntentRepositoryBinding binding,
        string reason,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return writer.MarkFailedAsync(binding, reason, ct);
    }

    private async Task<CloneRunResult> ExecuteCloneAsync(
        IntentRepositoryBinding binding,
        IGitProvider provider,
        CancellationToken ct)
    {
        var error = await TryCloneAsync(provider, binding, ct);
        if (error is not null)
        {
            await writer.MarkFailedAsync(binding, error, ct);
            return CloneRunResult.Failed;
        }

        await writer.MarkReadyAsync(binding, ct);
        return CloneRunResult.Ready;
    }

    private static async Task<string?> TryCloneAsync(
        IGitProvider provider,
        IntentRepositoryBinding binding,
        CancellationToken ct)
    {
        try
        {
            await provider.CloneRepositoryAsync(
                binding.Coordinate.Owner,
                binding.Coordinate.Repo,
                binding.WorkspacePath,
                ct);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Worker is shutting down; leave the binding in cloning so the next
            // process's recovery pass flips it to failed("interrupted"). Exactly
            // the path parent Q1 asks for, avoids racing two emitters.
            throw;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}

public enum CloneRunResult
{
    /// <summary>Binding flipped <c>pending → cloning → ready</c>.</summary>
    Ready,

    /// <summary>Binding flipped <c>pending → cloning → failed</c>.</summary>
    Failed,

    /// <summary>The id arrived in the queue but no binding exists (deleted between enqueue and dequeue).</summary>
    NotFound,

    /// <summary>Binding was already past <c>pending</c> (duplicate enqueue, manual replay, …) — no-op.</summary>
    AlreadyProcessed,

    /// <summary>Provider key on the binding is no longer registered; binding marked failed.</summary>
    ProviderMissing,
}
