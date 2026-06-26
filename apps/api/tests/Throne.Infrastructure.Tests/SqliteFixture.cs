using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Manifest;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore;

namespace Throne.Infrastructure.Tests;

public sealed class SqliteFixture : IAsyncLifetime
{
    private readonly List<SqliteTestDatabase> _databases = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task<SqliteTestDatabase> CreateDatabaseAsync()
    {
        var dataSource = Path.Combine(Path.GetTempPath(), $"throne-test-{Guid.NewGuid():N}.db");
        var provider = BuildProvider(dataSource);
        var initializer = provider.GetServices<IHostedService>().OfType<EfSchemaInitializer>().Single();
        await initializer.StartAsync(CancellationToken.None);

        var database = new SqliteTestDatabase(dataSource, provider);
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

    private static ServiceProvider BuildProvider(string dataSource)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EfPersistenceOptions.SectionName}:DataSource"] = dataSource,
            })
            .Build();

        var skillCatalog = Substitute.For<ISessionSkillCatalog>();
        skillCatalog.List().Returns(Array.Empty<SessionSkillDescriptor>());

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        services.AddSingleton<ISkillManifestProvider>(
            new InMemorySkillManifestProvider(new SkillManifest(1, [], [], [])));
        services.AddSingleton(skillCatalog);
        services.AddSingleton(Substitute.For<ITerminalVendorCatalog>());
        services.AddThroneEfCore(configuration);
        return services.BuildServiceProvider();
    }
}

public sealed class SqliteTestDatabase(string dataSource, ServiceProvider services) : IAsyncDisposable
{
    private bool _disposed;

    public string DataSource { get; } = dataSource;

    public IServiceProvider Services { get; } = services;

    public IDbContextFactory<ThroneDbContext> ContextFactory =>
        Services.GetRequiredService<IDbContextFactory<ThroneDbContext>>();

    public T GetRequiredService<T>() where T : notnull => Services.GetRequiredService<T>();

    public async Task<ThroneDbContext> CreateContextAsync() =>
        await ContextFactory.CreateDbContextAsync(CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await services.DisposeAsync();
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
