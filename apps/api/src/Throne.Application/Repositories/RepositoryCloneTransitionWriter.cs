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
    public Task<RepositoryCloneTransitionOutcome> MarkCloningAsync(
        IntentRepositoryBinding binding, CancellationToken ct) =>
        SaveAsync(binding, b => b.MarkCloning(clock.GetUtcNow()), ct);

    public Task<RepositoryCloneTransitionOutcome> MarkReadyAsync(
        IntentRepositoryBinding binding, CancellationToken ct) =>
        SaveAsync(binding, b => b.MarkReady(clock.GetUtcNow()), ct);

    public Task<RepositoryCloneTransitionOutcome> MarkFailedAsync(
        IntentRepositoryBinding binding, string error, CancellationToken ct) =>
        SaveAsync(binding, b => b.MarkFailed(error, clock.GetUtcNow()), ct);

    private Task<RepositoryCloneTransitionOutcome> SaveAsync(
        IntentRepositoryBinding binding,
        Action<IntentRepositoryBinding> transition,
        CancellationToken ct)
    {
        transition(binding);
        return unitOfWork.ExecuteAsync(
            async inner =>
            {
                var outcome = await bindings.SaveAsync(binding, inner);
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
