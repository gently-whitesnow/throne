using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Throne.Application.TaskTrackers;
using Throne.Application.TaskTrackers.Sync;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Http;
using Throne.Infrastructure.TaskTrackers.Sync;

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

        services.AddSingleton<KaitenRateLimiter>();
        services.AddSingleton<KaitenRetryPolicy>();
        services.AddSingleton<KaitenHttpExecutor>();
        services.AddSingleton<IKaitenTopologyApi, KaitenTopologyApi>();
        services.AddSingleton<IKaitenCardsApi, KaitenCardsApi>();
        services.AddSingleton<IKaitenCommentsApi, KaitenCommentsApi>();
        services.AddSingleton<IKaitenTagsApi, KaitenTagsApi>();
        services.AddSingleton<IKaitenCardChildrenApi, KaitenCardChildrenApi>();
        services.AddSingleton<IKaitenClient, KaitenClient>();

        services.AddSingleton<ITaskTrackerProvider, KaitenTaskTrackerProvider>();

        // Two-way sync (ADR-0008): the Kaiten card I/O surface plus the poll-loop host. The sync
        // provider is registered only as ITaskTrackerSyncProvider so it does not collide with the
        // catalog provider's "kaiten" registry key.
        var syncOptions = services.AddOptions<TaskTrackerSyncOptions>();
        if (configuration is not null)
        {
            syncOptions.Bind(configuration.GetSection(TaskTrackerSyncOptions.SectionName));
        }

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<TaskTrackerSyncOptions>>().Value);
        services.AddSingleton<ITaskTrackerSyncProvider, KaitenSyncProvider>();
        services.AddHostedService<TaskTrackerSyncService>();
    }
}
