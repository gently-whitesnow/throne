using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Infrastructure.EfCore.Bindings;
using Throne.Infrastructure.EfCore.Intents;
using Throne.Infrastructure.EfCore.PromptParts;
using Throne.Infrastructure.EfCore.Tags;
using Throne.Infrastructure.PromptParts;
using Throne.Infrastructure.Terminals;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// SQLite/EF Core persistence wiring. Registers the unit of work,
/// ambient-context accessor, pooled context factory, the startup schema initializer and
/// every repository port. The whole graph is Singleton:
/// <see cref="ThroneDbContext"/> instances are pulled from the Singleton
/// <see cref="IDbContextFactory{TContext}"/> into the ambient <c>AsyncLocal</c>, never
/// injected directly.
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

        services.AddSingleton<IIntentEventRepository, EfIntentEventRepository>();
        services.AddSingleton<ITextVersionRepository, EfTextVersionRepository>();
        services.AddSingleton<IIntentLinkRepository, EfIntentLinkRepository>();
        services.AddSingleton<IIntentPinRepository, EfIntentPinRepository>();
        services.AddSingleton<IIntentAttachmentRepository, EfIntentAttachmentRepository>();

        // Decomposed pieces backing the composite IIntentRepository — internal singletons
        // so the composite picks them up via DI without manual wiring.
        services.AddSingleton<EfIntentReader>();
        services.AddSingleton<EfIntentLifecycle>();
        services.AddSingleton<EfIntentTextEditor>();
        services.AddSingleton<EfIntentStatusMutator>();
        services.AddSingleton<EfIntentTagsMutator>();
        services.AddSingleton<EfIntentOrderingMutator>();
        services.AddSingleton<EfIntentContextReader>();
        services.AddSingleton<EfIntentRepository>();
        services.AddSingleton<IIntentRepository>(sp => sp.GetRequiredService<EfIntentRepository>());
        services.AddSingleton<IIntentOrderingRepository>(sp => sp.GetRequiredService<EfIntentRepository>());
        services.AddSingleton<ISystemIntentStatusWriter>(sp => sp.GetRequiredService<EfIntentRepository>());

        services.AddSingleton<EfTagLifecycle>();
        services.AddSingleton<EfTagMutator>();
        services.AddSingleton<EfTagListReader>();
        services.AddSingleton<EfTagDeletion>();
        services.AddSingleton<EfTagRepository>();
        services.AddSingleton<ITagRepository>(sp => sp.GetRequiredService<EfTagRepository>());

        // Manifest-backed decorator handles synthetic system parts; the EF store owns
        // user-scoped rows.
        services.AddSingleton<EfPromptPartLifecycle>();
        services.AddSingleton<EfPromptPartTextEditor>();
        services.AddSingleton<EfPromptPartRepository>();
        services.AddSingleton<IPromptPartRepository>(sp => new ManifestBackedPromptPartRepository(
            sp.GetRequiredService<ISkillManifestProvider>(),
            sp.GetRequiredService<EfPromptPartRepository>()));
        services.AddSingleton<EfPromptPartPatchLifecycle>();
        services.AddSingleton<EfPromptPartPatchDecisionMutator>();
        services.AddSingleton<IPromptPartPatchRepository, EfPromptPartPatchRepository>();

        services.AddSingleton<IDreamSessionRepository, EfDreamSessionRepository>();
        services.AddSingleton<IRepositoryRegistry, EfRepositoryRegistry>();
        services.AddSingleton<IPullRequestArtifactRepository, EfPullRequestArtifactStore>();
        services.AddSingleton<EfBindingReader>();
        services.AddSingleton<EfBindingLifecycle>();
        services.AddSingleton<EfIntentRepositoryBindingStore>();
        services.AddSingleton<IIntentRepositoryBindingRepository>(sp =>
            sp.GetRequiredService<EfIntentRepositoryBindingStore>());
        services.AddSingleton<ICapabilitiesRepository, EfCapabilitiesRepository>();
        services.AddSingleton<ITerminalSettingsStore, EfTerminalSettingsStore>();
        services.AddSingleton<IIntentTerminalLaunchStore, EfIntentTerminalLaunchStore>();
        services.AddSingleton<ISkillModeDefaultStore, EfSkillModeDefaultStore>();
        services.AddSingleton<IGitLabHostProvider, EfGitLabHostProvider>();
        services.AddSingleton<ITaskTrackerConnectionStore, EfTaskTrackerConnectionStore>();
        services.AddSingleton<ITaskTrackerCardLinkStore, EfTaskTrackerCardLinkStore>();

        services.AddHostedService<EfSchemaInitializer>();
        services.AddHostedService<SkillModeDefaultSeeder>();

        return services;
    }
}
