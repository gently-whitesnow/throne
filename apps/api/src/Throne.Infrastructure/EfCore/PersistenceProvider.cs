using Microsoft.Extensions.Configuration;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Reads the <c>Persistence:Provider</c> switch that picks the storage backend at
/// composition time. Default is <see cref="Mongo"/>: anything other than the literal
/// <see cref="Sqlite"/> (including an absent value) keeps the current Mongo wiring,
/// so existing deployments and tests are unaffected.
/// </summary>
internal static class PersistenceProvider
{
    public const string Key = "Persistence:Provider";
    public const string Mongo = "mongo";
    public const string Sqlite = "sqlite";

    public static bool IsSqlite(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return string.Equals(configuration[Key], Sqlite, StringComparison.OrdinalIgnoreCase);
    }
}
