using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Infrastructure.Git;
using Throne.Infrastructure.Mongo.Repositories;
using Throne.Infrastructure.PromptParts;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Mongo persistence wiring: client/database (<see cref="MongoModule"/>), the unit of
/// work + session accessor, every repository port, and the index/seed hosted services.
/// Selected by <c>Persistence:Provider=mongo</c> (the default). Lifted out of the root
/// composite so the provider branch in <c>DependencyInjection</c> stays flat and the
/// SQLite branch is a symmetric peer.
/// </summary>
internal static class MongoPersistenceModule
{
    public static IServiceCollection AddThroneMongoPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddThroneMongo(configuration);

        services.AddSingleton<MongoSessionAccessor>();
        services.AddSingleton<MongoUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(sp => new DomainEventDispatchingUnitOfWork(
            sp.GetRequiredService<MongoUnitOfWork>(),
            sp.GetRequiredService<IDomainEventDispatcher>()));
        services.AddSingleton<MongoIntentRepository>();
        services.AddSingleton<IIntentRepository>(sp => sp.GetRequiredService<MongoIntentRepository>());
        services.AddSingleton<IIntentOrderingRepository>(sp => sp.GetRequiredService<MongoIntentRepository>());
        services.AddSingleton<ISystemIntentStatusWriter>(sp => sp.GetRequiredService<MongoIntentRepository>());
        services.AddSingleton<IIntentPinRepository, MongoIntentPinRepository>();
        services.AddSingleton<IIntentLinkRepository, MongoIntentLinkRepository>();
        services.AddSingleton<ITagRepository, MongoTagRepository>();
        services.AddSingleton<IIntentAttachmentRepository, MongoIntentAttachmentRepository>();
        services.AddSingleton<MongoPromptPartRepository>();
        services.AddSingleton<IPromptPartRepository>(sp => new ManifestBackedPromptPartRepository(
            sp.GetRequiredService<ISkillManifestProvider>(),
            sp.GetRequiredService<MongoPromptPartRepository>()));
        services.AddSingleton<ITextVersionRepository, MongoTextVersionRepository>();
        services.AddSingleton<IIntentEventRepository, MongoIntentEventRepository>();
        services.AddSingleton<IPromptPartPatchRepository, MongoPromptPartPatchRepository>();
        services.AddSingleton<IDreamSessionRepository, MongoDreamSessionRepository>();
        services.AddSingleton<IIntentRepositoryBindingRepository, MongoIntentRepositoryBindingStore>();
        services.AddSingleton<IRepositoryRegistry, MongoRepositoryRegistry>();
        services.AddSingleton<IPullRequestArtifactRepository, MongoPullRequestArtifactStore>();
        services.AddSingleton<ICapabilitiesRepository, MongoCapabilitiesRepository>();
        services.AddSingleton<ITerminalSettingsStore, MongoTerminalSettingsStore>();
        services.AddSingleton<IIntentTerminalLaunchStore, MongoIntentTerminalLaunchStore>();
        services.AddSingleton<ISkillModeDefaultStore, MongoSkillModeDefaultStore>();
        // IGitLabHostProvider lives with the persistence module: the Mongo provider stores
        // its singleton in Mongo, the SQLite branch wires its EF Core counterpart instead.
        services.AddSingleton<IGitLabHostProvider, MongoGitLabHostProvider>();
        services.AddHostedService<MongoIndexInitializer>();
        services.AddHostedService<SkillModeDefaultSeeder>();

        return services;
    }
}
