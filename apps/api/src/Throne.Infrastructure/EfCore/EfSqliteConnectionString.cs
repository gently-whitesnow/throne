namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Builds the Microsoft.Data.Sqlite connection string from <see cref="EfPersistenceOptions"/>.
/// Shared by the runtime factory and the design-time factory so both target the same file.
/// </summary>
internal static class EfSqliteConnectionString
{
    public static string For(EfPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return $"Data Source={options.ResolveDataSourcePath()}";
    }
}
