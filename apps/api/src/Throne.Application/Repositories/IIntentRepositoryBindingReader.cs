using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read-only projection of <see cref="RepositoryBindingService.ListByIntentAsync"/> for
/// the MCP read paths (T-13). Pulled out as a separate port so MCP tool tests can stub
/// the binding listing without instantiating the full <see cref="RepositoryBindingService"/>
/// graph (resolver / persistence / sync workflow / clone queue / workspace provider).
/// Production wiring forwards to the existing service so the binding-ownership /
/// intent-existence semantics stay symmetric across HTTP and MCP surfaces.
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
