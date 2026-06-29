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
/// schema that does not exist yet. It also ensures the parent directory exists and puts
/// the database into WAL journal mode (a persistent, one-time property of the file).
/// </summary>
internal sealed partial class EfSchemaInitializer(
    IDbContextFactory<ThroneDbContext> contextFactory,
    IOptions<EfPersistenceOptions> options,
    ILogger<EfSchemaInitializer> log) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var path = options.Value.ResolveDataSourcePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);

        LogSchemaReady(log, path);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "SQLite schema ready (WAL): {DataSource}.")]
    private static partial void LogSchemaReady(ILogger logger, string dataSource);
}
