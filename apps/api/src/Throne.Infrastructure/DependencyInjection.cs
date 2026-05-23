using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Throne.Application.Events;
using Throne.Application.Git;
using Throne.Application.Instructions.Manifest;
using Throne.Application.Intents.Attachments;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Infrastructure.Git;
using Throne.Infrastructure.Git.GitHubCli;
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

        AddGitInfrastructure(services, configuration);

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
        services.AddSingleton<IPullRequestCommentRepository, MongoPullRequestCommentStore>();
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
        AddGitInfrastructure(services, configuration: null);
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
        services.AddSingleton<IPullRequestCommentRepository, MongoPullRequestCommentStore>();
        services.AddHostedService<MongoIndexInitializer>();
        // Run the cut-over backfill after the index initializer so the unique index on
        // (intent_id, version) is in place before the writer ever races a runtime edit.
        services.AddHostedService<MongoIntentEventsMigration>();
        return services;
    }

    /// <summary>
    /// Git provider shell-out plumbing (ADR-0024 / T-05): workspace root initializer,
    /// process launcher, provider registry and concrete providers. T-06 wires up
    /// <see cref="GitHubCliProvider"/> for the <c>github</c> key; PR operations land
    /// in T-07 on the same instance.
    /// </summary>
    private static void AddGitInfrastructure(IServiceCollection services, IConfiguration? configuration)
    {
        var workspaceBuilder = services.AddOptions<WorkspaceOptions>();
        var githubBuilder = services.AddOptions<GitHubCliOptions>();
        var prSyncBuilder = services.AddOptions<PullRequestSyncOptions>();
        if (configuration is not null)
        {
            workspaceBuilder.Bind(configuration.GetSection(WorkspaceOptions.SectionName));
            githubBuilder.Bind(configuration.GetSection(GitHubCliOptions.SectionName));
            prSyncBuilder.Bind(configuration.GetSection(PullRequestSyncOptions.SectionName));
        }
        // Application layer takes the bound options value directly so its
        // PullRequestSyncBackoff stays free of Microsoft.Extensions.Options.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<PullRequestSyncOptions>>().Value);

        services.AddSingleton<WorkspaceRootInitializer>();
        services.AddHostedService(sp => sp.GetRequiredService<WorkspaceRootInitializer>());
        services.AddSingleton<IWorkspaceRootProvider>(sp => sp.GetRequiredService<WorkspaceRootInitializer>());
        services.AddSingleton<IProcessLauncher, ProcessRunner>();
        services.AddSingleton<GhCliInvoker>();
        services.AddSingleton<GhRepoListExecutor>();
        services.AddSingleton<GhRepoSearcher>();
        services.AddSingleton<GhRepoActions>();
        services.AddSingleton<GhAuthProbe>();
        services.AddSingleton<GhPullRequestActions>();
        services.AddSingleton<IGitProvider, GitHubCliProvider>();
        services.AddSingleton<IGitProviderRegistry, GitProviderRegistry>();
        // T-09 RepositoryCloneService: consumer of the clone-on-bind queue, runs the
        // recovery pass on boot and drains BindingIds pushed by T-08. Workflow + queue
        // live in Application so this Infrastructure type stays thin.
        services.AddHostedService<RepositoryCloneService>();
        // T-10 PullRequestSyncService: polls open PR bindings on a tick, refreshes
        // upstream state + comments through the shared sync workflow. Tick body lives
        // in Application; this host stays a thin BackgroundService wrapper.
        services.AddHostedService<PullRequestSyncService>();
    }
}
