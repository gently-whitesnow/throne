using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Infrastructure.EfCore;

namespace Throne.Api.Tests.Infrastructure;

public sealed class SqliteFixture : IAsyncLifetime
{
    private readonly List<SqliteTestDatabase> _databases = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public SqliteTestDatabase CreateDatabase()
    {
        var dataSource = Path.Combine(Path.GetTempPath(), $"throne-test-{Guid.NewGuid():N}.db");
        var database = new SqliteTestDatabase(dataSource);
        _databases.Add(database);
        return database;
    }

    public async Task DisposeAsync()
    {
        foreach (var database in _databases)
        {
            await database.DisposeAsync();
        }
    }
}

public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private bool _disposed;
    private readonly string? _previousProvider;
    private readonly string? _previousDataSource;

    public SqliteTestDatabase(string dataSource)
    {
        DataSource = dataSource;
        _previousProvider = Environment.GetEnvironmentVariable("Persistence__Provider");
        _previousDataSource = Environment.GetEnvironmentVariable("Persistence__Sqlite__DataSource");
        Environment.SetEnvironmentVariable("Persistence__Provider", "sqlite");
        Environment.SetEnvironmentVariable("Persistence__Sqlite__DataSource", dataSource);
    }

    public string DataSource { get; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Environment.SetEnvironmentVariable("Persistence__Provider", _previousProvider);
        Environment.SetEnvironmentVariable("Persistence__Sqlite__DataSource", _previousDataSource);
        await Task.Yield();
        DeleteIfExists(DataSource);
        DeleteIfExists($"{DataSource}-wal");
        DeleteIfExists($"{DataSource}-shm");
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

[CollectionDefinition(nameof(SqliteIntegrationFixture))]
public sealed class SqliteIntegrationFixture : ICollectionFixture<SqliteFixture>
{
}

internal static class SqliteTestHost
{
    public static WebApplicationFactory<Program> Create(SqliteTestDatabase database) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            Configure(builder, database.DataSource));

    public static void Configure(
        IWebHostBuilder builder,
        string dataSource,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null)
    {
        builder.UseEnvironment("Production");
        builder.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = false;
            o.ValidateOnBuild = false;
        });
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Persistence:Provider"] = "sqlite",
                [$"{EfPersistenceOptions.SectionName}:DataSource"] = dataSource,
            };
            if (extraConfiguration is not null)
            {
                foreach (var (key, value) in extraConfiguration)
                {
                    values[key] = value;
                }
            }
            cfg.AddInMemoryCollection(values);
        });
        builder.ConfigureTestServices(services =>
            services.PostConfigure<EfPersistenceOptions>(options => options.DataSource = dataSource));
    }
}
