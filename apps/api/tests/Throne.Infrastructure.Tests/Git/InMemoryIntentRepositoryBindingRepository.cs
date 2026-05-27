using System.Collections.Concurrent;
using Throne.Application.Ports;
using Throne.Domain.Intents;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Tests.Git;

/// <summary>
/// Тестовый in-memory store binding'ов для прогонов
/// <see cref="Throne.Infrastructure.Git.RepositoryCloneService"/> без MongoDB.
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

    public Task<IntentRepositoryBinding?> GetByIdAsync(BindingId id, CancellationToken ct) =>
        Task.FromResult(_byId.TryGetValue(id, out var b) ? b : null);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByIntentAsync(IntentId intentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(
            _byId.Values.Where(b => b.IntentId.Equals(intentId)).ToArray());

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindOpenForSyncAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>([]);

    public Task<IReadOnlyList<IntentRepositoryBinding>> FindByCloneStatusAsync(string cloneStatus, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntentRepositoryBinding>>(
            _byId.Values.Where(b => b.State.CloneStatus == cloneStatus).ToArray());

    public Task<SaveBindingOutcome> SaveAsync(IntentRepositoryBinding binding, CancellationToken ct)
    {
        _byId[binding.Id] = binding;
        return Task.FromResult<SaveBindingOutcome>(new SaveBindingOutcome.Saved(binding));
    }

    public Task<DeleteBindingOutcome> DeleteAsync(BindingId id, CancellationToken ct)
    {
        _byId.TryRemove(id, out var b);
        return Task.FromResult<DeleteBindingOutcome>(
            b is null ? new DeleteBindingOutcome.NotFound() : new DeleteBindingOutcome.Deleted(b));
    }
}
