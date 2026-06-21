using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read-only projection of <see cref="RepositoryBindingService.ListByIntentAsync"/>.
/// Pulled out as a separate port so callers can list bindings without instantiating
/// the full <see cref="RepositoryBindingService"/> graph. Production wiring forwards
/// to the existing service so the binding-ownership / intent-existence semantics
/// stay symmetric across HTTP surfaces.
/// </summary>
public interface IIntentRepositoryBindingReader
{
    Task<IReadOnlyList<IntentRepositoryBinding>> ListByIntentAsync(string intentId, CancellationToken ct);
}

internal sealed class IntentRepositoryBindingReader(RepositoryBindingService service) : IIntentRepositoryBindingReader
{
    public Task<IReadOnlyList<IntentRepositoryBinding>> ListByIntentAsync(string intentId, CancellationToken ct) =>
        service.ListByIntentAsync(intentId, ct);
}
