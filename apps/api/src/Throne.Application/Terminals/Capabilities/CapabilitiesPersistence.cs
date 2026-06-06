using Throne.Application.Ports;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Terminals.Capabilities;

/// <summary>
/// Read + upsert helper for the <see cref="CapabilitiesAggregate"/> singleton,
/// keeping the unit-of-work choreography in one place.
/// </summary>
public sealed class CapabilitiesPersistence(
    ICapabilitiesRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public Task<CapabilitiesAggregate?> GetAsync(CancellationToken ct) =>
        repository.GetAsync(ct);

    public Task UpsertToggleAsync(string name, bool enabled, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return unitOfWork.ExecuteAsync(inner => WriteToggleAsync(name, enabled, now, inner), ct);
    }

    private async Task WriteToggleAsync(string name, bool enabled, DateTimeOffset now, CancellationToken ct)
    {
        var existed = await repository.GetAsync(ct);
        var current = existed ?? CapabilitiesAggregate.CreateEmpty(now);
        // SetEnabled returns false when the value would not change — skip the write
        // to avoid bumping current_version on a no-op PUT. Empty-aggregate path
        // always saves so the singleton row materialises lazily.
        var changed = current.SetEnabled(name, enabled, now);
        if (changed || existed is null)
        {
            await repository.SaveAsync(current, ct);
        }
    }
}
