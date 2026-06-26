using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Throne.Application.Intents.Attachments;
using Throne.Application.LocalModels;
using Throne.Application.Manifest;
using Throne.Application.Ports;
using Throne.Infrastructure.EfCore;
using Throne.Infrastructure.Git;
using Throne.Infrastructure.Imaging;
using Throne.Infrastructure.LocalModels;
using Throne.Infrastructure.Manifest;
using Throne.Infrastructure.Tokenization;

namespace Throne.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddThroneInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddThroneEfCore(configuration);

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

        services.AddSingleton<ITokenizer, SharpTokenTokenizer>();
        services.AddSingleton<IImageDownscaler, ImageSharpDownscaler>();
        services.AddOptions<IntentAttachmentCompressionOptions>()
            .BindConfiguration(IntentAttachmentCompressionOptions.SectionName);
        services.AddHostedService<IntentAttachmentCompressionWorker>();

        return services;
    }
}
