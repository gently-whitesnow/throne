using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Throne.Application.Events;
using Throne.Application.Intents.Attachments;
using Throne.Application.LocalModels;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Infrastructure.Git;
using Throne.Infrastructure.Imaging;
using Throne.Infrastructure.LocalModels;
using Throne.Infrastructure.Manifest;
using Throne.Infrastructure.Mongo;
using Throne.Infrastructure.Mongo.Repositories;
using Throne.Infrastructure.Tokenization;

namespace Throne.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<MongoOptions>()
            .Bind(configuration.GetSection(MongoOptions.SectionName));

        services.AddOptions<SkillManifestOptions>()
            .Bind(configuration.GetSection(SkillManifestOptions.SectionName));
        services.AddSingleton<ISkillManifestProvider, YamlFileSkillManifestProvider>();

        GitInfrastructureModule.AddThroneGitInfrastructure(services, configuration);
        Throne.Infrastructure.Terminals.TerminalsModule.AddThroneTerminalsInfrastructure(services, configuration);

        services.AddOptions<LocalModelSettings>()
            .Bind(configuration.GetSection(LocalModelSettings.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LocalModelSettings>>().Value);
        services.AddHttpClient(LocalModelCatalogHttpClient.HttpClientName);
        services.AddSingleton<ILocalModelCatalogPort, LocalModelCatalogHttpClient>();

        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));

        services.AddSingleton<IMongoDatabase>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(opts.Database);
        });

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
        services.AddSingleton<IPromptPartRepository, MongoPromptPartRepository>();
        services.AddSingleton<ITextVersionRepository, MongoTextVersionRepository>();
        services.AddSingleton<IIntentEventRepository, MongoIntentEventRepository>();
        services.AddSingleton<IMcpCallLogSink, MongoMcpCallLogSink>();
        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddSingleton<IImageDownscaler, ImageSharpDownscaler>();
        services.AddOptions<IntentAttachmentCompressionOptions>()
            .BindConfiguration(IntentAttachmentCompressionOptions.SectionName);
        services.AddHostedService<IntentAttachmentCompressionWorker>();
        services.AddSingleton<IPromptPartPatchRepository, MongoPromptPartPatchRepository>();
        services.AddSingleton<IDreamSessionRepository, MongoDreamSessionRepository>();
        services.AddSingleton<IIntentRepositoryBindingRepository, MongoIntentRepositoryBindingStore>();
        services.AddSingleton<IRepositoryRegistry, MongoRepositoryRegistry>();
        services.AddSingleton<IRepositoryArtifactRepository, MongoRepositoryArtifactStore>();
        services.AddSingleton<ICapabilitiesRepository, MongoCapabilitiesRepository>();
        services.AddSingleton<ITerminalSettingsStore, MongoTerminalSettingsStore>();
        services.AddHostedService<MongoIndexInitializer>();
        // PromptPartSeeder runs after the index initializer: it relies on the (scope,key) unique
        // index being in flight and reconciles system parts from the skill manifest (ADR-0036).
        services.AddHostedService<PromptPartSeeder>();

        return services;
    }
}
