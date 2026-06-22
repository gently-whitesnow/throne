using Throne.Application.Ports;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Read + upsert helper for the <see cref="CapabilitiesAggregate"/> singleton. The
/// singleton row materialises lazily on first write.
/// </summary>
public sealed class CapabilitiesPersistence(
    ICapabilitiesRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<CapabilitiesAggregate?> GetAsync(CancellationToken ct) =>
        repository.GetAsync(ct);

    /// <summary>
    /// Persist the new <paramref name="provider"/> selection for capability
    /// <paramref name="name"/>. Pass <c>null</c> to clear. Returns <c>true</c> when the
    /// stored value actually changed or when the singleton row was just created.
    /// </summary>
    public Task<bool> UpsertSelectionAsync(string name, string? provider, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return unitOfWork.ExecuteAsync(inner => WriteSelectionAsync(name, provider, now, inner), ct);
    }

    private async Task<bool> WriteSelectionAsync(string name, string? provider, DateTimeOffset now, CancellationToken ct)
    {
        var existed = await repository.GetAsync(ct);
        var current = existed ?? CapabilitiesAggregate.CreateEmpty(now);
        var changed = current.SetSelectedProvider(name, provider, now);
        if (changed || existed is null)
        {
            await repository.SaveAsync(current, ct);
            return true;
        }
        return false;
    }
}
