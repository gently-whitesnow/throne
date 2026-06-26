using System.Collections.Concurrent;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Тестовый in-memory store binding'ов для прогонов
/// <see cref="Throne.Infrastructure.Git.RepositoryCloneService"/> без внешней БД.
/// Поддерживает только методы, которыми реально пользуются workflow + recovery.
/// </summary>
internal sealed class InMemoryIntentRepositoryBindingRepository : IIntentRepositoryBindingRepository
{
    private readonly ConcurrentDictionary<BindingId, IntentRepositoryBinding> _byId = new();

    public void Seed(IntentRepositoryBinding binding) => _byId[binding.Id] = binding;

    public Task<CreateBindingOutcome> CreateAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        _byId[binding.Id] = binding;
        return Task.FromResult<CreateBindingOutcome>(new CreateBindingOutcome.Created(binding));
    }

    public Task<IntentRepositoryBinding?> GetByIdAsync(BindingId id, CancellationToken ct)
    {
        _byId.TryGetValue(id, out var b);
        return Task.FromResult(b);
    }

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(IntentId intentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(
            _byId.Values.Where(b => b.IntentId.Equals(intentId)).ToArray());

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(_byId.Values.ToArray());

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([]);

    // Not exercised by the clone-service/recovery harness — returns empty like
    // FindOpenForSyncAsync (the auto-bind pass is unit-tested against an NSubstitute mock).
    public Task<IReadOnlyList<IntentRepositoryBinding>> FindReadyWithoutPullRequestAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([]);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByCloneStatusAsync(string cloneStatus, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(
            _byId.Values.Where(b => b.State.CloneStatus == cloneStatus).ToArray());

    public Task<SaveBindingOutcome> SaveAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        _byId[binding.Id] = binding;
        return Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(binding));
    }

    // The harness never races two workers on the *same* binding (parallel runner test uses
    // distinct pending bindings), so the fake doesn't model the lost-claim branch — that is
    // covered against an NSubstitute mock in RepositoryCloneWorkflowTests. Here a claim always
    // wins, identical to SaveAsync.
    public Task<SaveBindingOutcome> ClaimCloningAsync(IntentRepositoryBinding binding, CancellationToken ct) =>
        SaveAsync(binding, ct);

    public Task<DeleteBindingOutcome> DeleteAsync(BindingId id, CancellationToken ct)
    {
        _byId.TryRemove(id, out var b);
        return Task.FromResult<DeleteBindingOutcome>(
            b is null ? new DeleteBindingOutcome.NotFound() : new DeleteBindingOutcome.Deleted(b));
    }
}
