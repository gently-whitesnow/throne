using Throne.Domain.Capabilities;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class CapabilitiesRowMapper
{
    public static CapabilitiesRow ToRow(Capabilities capabilities) => new()
    {
        Id = Capabilities.SingletonId,
        CurrentVersion = capabilities.CurrentVersion,
        UpdatedAt = capabilities.UpdatedAt,
        Selections = new Dictionary<string, string>(capabilities.Selections, StringComparer.Ordinal),
    };

    public static Capabilities ToDomain(CapabilitiesRow row)
    {
        // Project Dictionary<string,string> into the nullable-valued shape the domain Restore
        // accepts; Restore drops stale selections so old rows survive a no-op upgrade.
        var selections = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, provider) in row.Selections)
        {
            selections[name] = provider;
        }
        return Capabilities.Restore(
            currentVersion: row.CurrentVersion < 1 ? 1 : row.CurrentVersion,
            updatedAt: row.UpdatedAt,
            selections: selections);
    }
}
