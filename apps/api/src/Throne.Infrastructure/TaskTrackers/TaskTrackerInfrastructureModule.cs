using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers.GenericHttp;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Http;

namespace Throne.Infrastructure.TaskTrackers;

/// <summary>
/// Composition for the task-tracker adapters in <see cref="Throne.Infrastructure"/>. The Kaiten
/// adapter plugs into the provider-neutral axis with a single
/// <c>AddSingleton&lt;ITaskTrackerProvider, …&gt;()</c> line (ADR-0045) — that is what surfaces it in
/// the catalog; the rest wires its native HTTP client (transport + typed endpoint groups).
/// </summary>
internal static class TaskTrackerInfrastructureModule
{
    public static void AddThroneTaskTrackerInfrastructure(
        IServiceCollection services,
        IConfiguration? configuration)
    {
        var kaitenOptions = services.AddOptions<KaitenOptions>();
        if (configuration is not null)
        {
            kaitenOptions.Bind(configuration.GetSection(KaitenOptions.SectionName));
        }

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<KaitenOptions>>().Value);

        services.AddHttpClient(KaitenHttpExecutor.HttpClientName, (sp, client) =>
            client.Timeout = TimeSpan.FromSeconds(sp.GetRequiredService<KaitenOptions>().RequestTimeoutSeconds));
        services.AddHttpClient(GenericHttpClient.HttpClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(30));

        services.AddSingleton<KaitenRateLimiter>();
        services.AddSingleton<KaitenRetryPolicy>();
        services.AddSingleton<KaitenHttpExecutor>();
        services.AddSingleton<IKaitenTopologyApi, KaitenTopologyApi>();
        services.AddSingleton<IKaitenCardsApi, KaitenCardsApi>();
        services.AddSingleton<IKaitenCommentsApi, KaitenCommentsApi>();
        services.AddSingleton<IKaitenTagsApi, KaitenTagsApi>();
        services.AddSingleton<IKaitenCardChildrenApi, KaitenCardChildrenApi>();
        services.AddSingleton<IKaitenClient, KaitenClient>();
        services.AddSingleton<GenericHttpClient>();

        services.AddSingleton<ITaskTrackerProvider, KaitenTaskTrackerProvider>();
        services.AddSingleton<ITaskTrackerProvider, GenericHttpTaskTrackerProvider>();

        var healthProbeOptions = services.AddOptions<TaskTrackerHealthProbeOptions>();
        if (configuration is not null)
        {
            healthProbeOptions.Bind(configuration.GetSection(TaskTrackerHealthProbeOptions.SectionName));
        }

        services.AddHostedService<TaskTrackerHealthProbeService>();
    }
}
