using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Infrastructure.EfCore;

namespace Throne.Infrastructure.Tests.EfCore;

public sealed class EfSqliteProvisioningTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"throne-ef-{Guid.NewGuid():N}");

    private string DbPath => Path.Combine(_root, "nested", "throne.db");

    [Fact(DisplayName = "AddThroneEfCore регистрирует UoW/контекст/schema-init для sqlite-ветки")]
    public void AddThroneEfCore_registers_unit_of_work_context_and_schema_initializer()
    {
        using var provider = BuildProvider();

        provider.GetService<IDbContextFactory<ThroneDbContext>>().Should().NotBeNull();
        provider.GetService<EfUnitOfWork>().Should().NotBeNull();
        provider.GetRequiredService<IUnitOfWork>().Should().BeOfType<DomainEventDispatchingUnitOfWork>();
        provider.GetServices<IHostedService>().OfType<EfSchemaInitializer>().Should().ContainSingle();
    }

    [Fact(DisplayName = "EfSchemaInitializer применяет миграцию на старте и создаёт БД (WAL)")]
    public async Task Schema_initializer_applies_migration_and_creates_database()
    {
        using var provider = BuildProvider();
        var initializer = provider.GetServices<IHostedService>().OfType<EfSchemaInitializer>().Single();

        await initializer.StartAsync(CancellationToken.None);

        File.Exists(DbPath).Should().BeTrue("schema init must create the SQLite file under its parent dir");

        var factory = provider.GetRequiredService<IDbContextFactory<ThroneDbContext>>();
        await using var context = await factory.CreateDbContextAsync(CancellationToken.None);

        var applied = await context.Database.GetAppliedMigrationsAsync(CancellationToken.None);
        applied.Should().Contain(id => id.EndsWith("InitialCreate", StringComparison.Ordinal));

        (await ReadJournalModeAsync(context)).Should().Be("wal");
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{EfPersistenceOptions.SectionName}:DataSource"] = DbPath,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(Substitute.For<IDomainEventDispatcher>());
        // SkillModeDefaultSeeder (hosted service) and EfTerminalSettingsStore depend on the
        // operational-skill and vendor catalogs from Application — substitute them so the DI
        // graph composes without the full Application module wired in.
        var skillCatalog = Substitute.For<ISessionSkillCatalog>();
        skillCatalog.List().Returns(System.Array.Empty<SessionSkillDescriptor>());
        services.AddSingleton(skillCatalog);
        services.AddSingleton(Substitute.For<ITerminalVendorCatalog>());
        // UserPromptSeedSeeder (hosted service) is constructed when IHostedService is
        // enumerated below; give it a TimeProvider and an empty seed provider so the DI
        // graph composes without the full Application/Infrastructure modules.
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<Throne.Application.Manifest.IUserPromptSeedProvider>(
            new Throne.Application.Manifest.InMemoryUserPromptSeedProvider(
                new Throne.Application.Manifest.UserPromptSeed(1, [])));
        services.AddThroneEfCore(configuration);
        return services.BuildServiceProvider();
    }

    private static async Task<string?> ReadJournalModeAsync(ThroneDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";
        return (string?)await command.ExecuteScalarAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
