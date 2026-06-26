using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Bindings;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Composite EF Core <see cref="IIntentRepositoryBindingRepository"/> (ADR-0024). Splits
/// the read-only listings (<see cref="EfBindingReader"/>) from the write/CAS surface
/// (<see cref="EfBindingLifecycle"/>) to keep each piece under the file budget; this shell
/// is purely delegating.
/// </summary>
internal sealed class EfIntentRepositoryBindingStore(
    EfBindingReader reader,
    EfBindingLifecycle lifecycle)
    : IIntentRepositoryBindingRepository
{
    public Task<CreateBindingOutcome> CreateAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return lifecycle.CreateAsync(binding, ct);
    }

    public Task<IntentRepositoryBinding?> GetByIdAsync(BindingId id, CancellationToken ct) =>
        reader.GetByIdAsync(id, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(
        IntentId intentId, CancellationToken ct) =>
        reader.FindByIntentAsync(intentId, ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindAllAsync(CancellationToken ct) =>
        reader.FindAllAsync(ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct) =>
        reader.FindOpenForSyncAsync(ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindReadyWithoutPullRequestAsync(
        CancellationToken ct) =>
        reader.FindReadyWithoutPullRequestAsync(ct);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByCloneStatusAsync(
        string cloneStatus, CancellationToken ct) =>
        reader.FindByCloneStatusAsync(cloneStatus, ct);

    public Task<SaveBindingOutcome> SaveAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return lifecycle.SaveAsync(binding, ct);
    }

    public Task<SaveBindingOutcome> ClaimCloningAsync(
        IntentRepositoryBinding binding, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return lifecycle.ClaimCloningAsync(binding, ct);
    }

    public Task<DeleteBindingOutcome> DeleteAsync(BindingId id, CancellationToken ct) =>
        lifecycle.DeleteAsync(id, ct);
}
