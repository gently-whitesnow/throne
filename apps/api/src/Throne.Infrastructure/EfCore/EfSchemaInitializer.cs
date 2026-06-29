using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Brings the schema up to date on startup via
/// <see cref="RelationalDatabaseFacadeExtensions.MigrateAsync"/>. It runs on the
/// startup critical path (<see cref="StartAsync"/>, not a
/// background loop) — the local file migrates in milliseconds and requests must not hit a
/// schema that does not exist yet. It also ensures the parent directory exists and applies
/// <see cref="EfPersistenceOptions.JournalMode"/> (a persistent, one-time property of the
/// file; WAL by default, DELETE for network-filesystem deployments).
/// </summary>
internal sealed partial class EfSchemaInitializer(
    IDbContextFactory<ThroneDbContext> contextFactory,
    IOptions<EfPersistenceOptions> options,
    ILogger<EfSchemaInitializer> log) : IHostedService
{
    private static readonly HashSet<string> AllowedJournalModes =
        new(StringComparer.OrdinalIgnoreCase) { "DELETE", "TRUNCATE", "PERSIST", "MEMORY", "WAL", "OFF" };

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = options.Value.ResolveDataSourcePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var journalMode = (options.Value.JournalMode ?? "WAL").Trim().ToUpperInvariant();
        if (!AllowedJournalModes.Contains(journalMode))
        {
            throw new InvalidOperationException(
                $"Invalid Persistence:Sqlite:JournalMode '{options.Value.JournalMode}'. Allowed: {string.Join(", ", AllowedJournalModes)}.");
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        // PRAGMA does not accept bound parameters; journalMode is whitelisted above, so
        // string concat here is the explicit-safe path past EF1002.
        var pragma = "PRAGMA journal_mode=" + journalMode + ";";
        await context.Database.ExecuteSqlRawAsync(pragma, cancellationToken);

        LogSchemaReady(log, journalMode, path);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "SQLite schema ready ({JournalMode}): {DataSource}.")]
    private static partial void LogSchemaReady(ILogger logger, string journalMode, string dataSource);
}
