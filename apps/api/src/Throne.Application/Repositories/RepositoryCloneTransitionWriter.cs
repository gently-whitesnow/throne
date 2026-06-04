using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Save-side helper that drives the <c>(transition → save → carrier)</c> pipeline:
/// pushes <see cref="RepositoryCloneTransitionOutcome"/> through
/// <see cref="IUnitOfWork.ExecuteAsync"/> so the dispatching unit-of-work fans
/// <see cref="Events.IntentRepositoryCloneProgress"/> out automatically.
/// </summary>
public sealed class RepositoryCloneTransitionWriter(
    IIntentRepositoryBindingRepository bindings,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <summary>
    /// Claims the binding for cloning via the atomic <c>pending → cloning</c> CAS
    /// (<see cref="IIntentRepositoryBindingRepository.ClaimCloningAsync"/>). A non-persisted
    /// outcome (<see cref="RepositoryCloneTransitionOutcome.WasPersisted"/> false) means a
    /// concurrent worker already owns the clone — the caller must skip it.
    /// </summary>
    public Task<RepositoryCloneTransitionOutcome> MarkCloningAsync(
        IntentRepositoryBinding binding, CancellationToken ct) =>
        PersistAsync(binding, b => b.MarkCloning(clock.GetUtcNow()), bindings.ClaimCloningAsync, ct);

    public Task<RepositoryCloneTransitionOutcome> MarkPendingRetryAsync(
        IntentRepositoryBinding binding, CancellationToken ct) =>
        SaveAsync(binding, b => b.MarkPendingRetry(clock.GetUtcNow()), ct);

    public Task<RepositoryCloneTransitionOutcome> MarkReadyAsync(
        IntentRepositoryBinding binding, CancellationToken ct) =>
        SaveAsync(binding, b => b.MarkReady(clock.GetUtcNow()), ct);

    public Task<RepositoryCloneTransitionOutcome> MarkFailedAsync(
        IntentRepositoryBinding binding, string error, CancellationToken ct) =>
        SaveAsync(binding, b => b.MarkFailed(error, clock.GetUtcNow()), ct);

    private Task<RepositoryCloneTransitionOutcome> SaveAsync(
        IntentRepositoryBinding binding,
        Action<IntentRepositoryBinding> transition,
        CancellationToken ct) =>
        PersistAsync(binding, transition, bindings.SaveAsync, ct);

    private Task<RepositoryCloneTransitionOutcome> PersistAsync(
        IntentRepositoryBinding binding,
        Action<IntentRepositoryBinding> transition,
        Func<IntentRepositoryBinding, CancellationToken, Task<SaveBindingOutcome>> persist,
        CancellationToken ct)
    {
        transition(binding);
        return unitOfWork.ExecuteAsync(
            async inner =>
            {
                var outcome = await persist(binding, inner);
                return outcome switch
                {
                    SaveBindingOutcome.Saved saved => RepositoryCloneTransitionOutcome.Persisted(saved.Binding),
                    SaveBindingOutcome.NotFound => RepositoryCloneTransitionOutcome.Vanished,
                    _ => throw new InvalidOperationException($"Unhandled outcome: {outcome.GetType().Name}"),
                };
            },
            ct);
    }
}
