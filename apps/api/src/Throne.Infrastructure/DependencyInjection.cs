using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Throne.Application.DreamRuns;
using Throne.Application.Events;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Ports;
using Throne.Infrastructure.Manifest;
using Throne.Infrastructure.Mongo;
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

        services.AddOptions<DreamOptions>()
            .Bind(configuration.GetSection(DreamOptions.SectionName));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DreamOptions>>().Value);

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
        services.AddSingleton<IIntentRepository, MongoIntentRepository>();
        services.AddSingleton<ITagRepository, MongoTagRepository>();
        services.AddSingleton<IIntentAttachmentRepository, MongoIntentAttachmentRepository>();
        services.AddSingleton<IIntentTrainingRepository, MongoIntentTrainingRepository>();
        services.AddSingleton<IInstructionRepository, MongoInstructionRepository>();
        services.AddSingleton<ITextVersionRepository, MongoTextVersionRepository>();
        services.AddSingleton<IMcpCallLogSink, MongoMcpCallLogSink>();
        services.AddSingleton<IDreamRunRepository, MongoDreamRunRepository>();
        services.AddSingleton<IIntentWindowQueries, MongoIntentWindowQueries>();
        services.AddSingleton<IFeedbackQueries, MongoFeedbackQueries>();
        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddHostedService<MongoIndexInitializer>();

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
        services.AddOptions<DreamOptions>();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<DreamOptions>>().Value);
        services.AddSingleton<MongoSessionAccessor>();
        services.AddSingleton<MongoUnitOfWork>();
        services.AddSingleton<IUnitOfWork>(sp => new DomainEventDispatchingUnitOfWork(
            sp.GetRequiredService<MongoUnitOfWork>(),
            sp.GetRequiredService<IDomainEventDispatcher>()));
        services.AddSingleton<IIntentRepository, MongoIntentRepository>();
        services.AddSingleton<ITagRepository, MongoTagRepository>();
        services.AddSingleton<IIntentAttachmentRepository, MongoIntentAttachmentRepository>();
        services.AddSingleton<IIntentTrainingRepository, MongoIntentTrainingRepository>();
        services.AddSingleton<IInstructionRepository, MongoInstructionRepository>();
        services.AddSingleton<ITextVersionRepository, MongoTextVersionRepository>();
        services.AddSingleton<IMcpCallLogSink, MongoMcpCallLogSink>();
        services.AddSingleton<IDreamRunRepository, MongoDreamRunRepository>();
        services.AddSingleton<IIntentWindowQueries, MongoIntentWindowQueries>();
        services.AddSingleton<IFeedbackQueries, MongoFeedbackQueries>();
        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddHostedService<MongoIndexInitializer>();
        return services;
    }
}

