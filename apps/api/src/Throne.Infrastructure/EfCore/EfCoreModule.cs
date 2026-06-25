using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Throne.Application.Events;
using Throne.Application.Ports;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// SQLite/EF Core persistence wiring (active when <c>Persistence:Provider=sqlite</c>).
/// Registers the unit of work, ambient-context accessor, pooled context factory and the
/// startup schema initializer — the same shape as the Mongo module, minus the repository
/// ports which land in slice 2. The whole graph is Singleton: <see cref="ThroneDbContext"/>
/// instances are pulled from the Singleton <see cref="IDbContextFactory{TContext}"/> into
/// the ambient <c>AsyncLocal</c>, never injected directly.
/// </summary>
internal static class EfCoreModule
{
    public static IServiceCollection AddThroneEfCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EfPersistenceOptions>()
            .Bind(configuration.GetSection(EfPersistenceOptions.SectionName));

        services.AddDbContextFactory<ThroneDbContext>((sp, builder) =>
        {
            var options = sp.GetRequiredService<IOptions<EfPersistenceOptions>>().Value;
            builder.UseSqlite(EfSqliteConnectionString.For(options));
        });

        services.AddSingleton<EfSessionAccessor>();
        services.AddSingleton<EfUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(sp => new DomainEventDispatchingUnitOfWork(
            sp.GetRequiredService<EfUnitOfWork>(),
            sp.GetRequiredService<IDomainEventDispatcher>()));

        services.AddHostedService<EfSchemaInitializer>();

        return services;
    }
}
