using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Throne.Application.Events;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Infrastructure.Imaging;
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
        services.AddSingleton<IIntentPinRepository, MongoIntentPinRepository>();
        services.AddSingleton<IIntentLinkRepository, MongoIntentLinkRepository>();
        services.AddSingleton<ITagRepository, MongoTagRepository>();
        services.AddSingleton<IIntentAttachmentRepository, MongoIntentAttachmentRepository>();
        services.AddSingleton<IInstructionRepository, MongoInstructionRepository>();
        services.AddSingleton<ITextVersionRepository, MongoTextVersionRepository>();
        services.AddSingleton<IIntentEventRepository, MongoIntentEventRepository>();
        services.AddSingleton<IMcpCallLogSink, MongoMcpCallLogSink>();
        services.AddSingleton<IPersonalAccessTokenRepository, MongoPersonalAccessTokenRepository>();
        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddSingleton<IImageDownscaler, ImageSharpDownscaler>();
        services.AddOptions<IntentAttachmentCompressionOptions>()
            .BindConfiguration(IntentAttachmentCompressionOptions.SectionName);
        services.AddHostedService<IntentAttachmentCompressionWorker>();
        services.AddSingleton<IInstructionPatchRepository, MongoInstructionPatchRepository>();
        services.AddSingleton<IDreamSessionRepository, MongoDreamSessionRepository>();
        services.AddSingleton<IIntentRepositoryBindingRepository, MongoIntentRepositoryBindingStore>();
        services.AddHostedService<MongoIndexInitializer>();
        // Run the cut-over backfill after the index initializer so the unique index on
        // (intent_id, version) is in place before the writer ever races a runtime edit.
        services.AddHostedService<MongoIntentEventsMigration>();

        return services;
    }

    public static IServiceCollection AddThroneInfrastructureWithDatabase(
        this IServiceCollection services,
        IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);

        services.AddSingleton(database);
        services.AddSingleton<IMongoClient>(database.Client);
        services.AddOptions<SkillManifestOptions>();
        services.AddSingleton<ISkillManifestProvider, YamlFileSkillManifestProvider>();
        services.AddSingleton<MongoSessionAccessor>();
        services.AddSingleton<MongoUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(sp => new DomainEventDispatchingUnitOfWork(
            sp.GetRequiredService<MongoUnitOfWork>(),
            sp.GetRequiredService<IDomainEventDispatcher>()));
        services.AddSingleton<MongoIntentRepository>();
        services.AddSingleton<IIntentRepository>(sp => sp.GetRequiredService<MongoIntentRepository>());
        services.AddSingleton<IIntentOrderingRepository>(sp => sp.GetRequiredService<MongoIntentRepository>());
        services.AddSingleton<IIntentPinRepository, MongoIntentPinRepository>();
        services.AddSingleton<IIntentLinkRepository, MongoIntentLinkRepository>();
        services.AddSingleton<ITagRepository, MongoTagRepository>();
        services.AddSingleton<IIntentAttachmentRepository, MongoIntentAttachmentRepository>();
        services.AddSingleton<IInstructionRepository, MongoInstructionRepository>();
        services.AddSingleton<ITextVersionRepository, MongoTextVersionRepository>();
        services.AddSingleton<IIntentEventRepository, MongoIntentEventRepository>();
        services.AddSingleton<IMcpCallLogSink, MongoMcpCallLogSink>();
        services.AddSingleton<IPersonalAccessTokenRepository, MongoPersonalAccessTokenRepository>();
        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddSingleton<IImageDownscaler, ImageSharpDownscaler>();
        services.AddOptions<IntentAttachmentCompressionOptions>();
        services.AddSingleton<IInstructionPatchRepository, MongoInstructionPatchRepository>();
        services.AddSingleton<IDreamSessionRepository, MongoDreamSessionRepository>();
        services.AddSingleton<IIntentRepositoryBindingRepository, MongoIntentRepositoryBindingStore>();
        services.AddHostedService<MongoIndexInitializer>();
        // Run the cut-over backfill after the index initializer so the unique index on
        // (intent_id, version) is in place before the writer ever races a runtime edit.
        services.AddHostedService<MongoIntentEventsMigration>();
        return services;
    }
}
