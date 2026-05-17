using Microsoft.Extensions.DependencyInjection;

namespace Throne.Api.Intents;

/// <summary>
/// DI registration for the 22 per-endpoint classes used by the four Intents
/// controllers (Intents / IntentPins / IntentLinks / IntentAttachments).
/// Endpoints take their dependencies through primary constructors and live as
/// Singletons next to the application handlers they wrap. Registering them
/// here keeps <see cref="Mcp.ThroneMcpCoreServices"/> readable while still
/// being called from the same composition root.
/// </summary>
internal static class IntentEndpointServices
{
    public static IServiceCollection AddThroneIntentEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Shared per-request helpers (tag map / pin lookups). The two
        // collaborators below hold the actual repository dependencies; the
        // facade exposes only the high-level public methods.
        services.AddSingleton<IntentsApiTagMap>();
        services.AddSingleton<IntentsApiPinLookup>();
        services.AddSingleton<IntentsApiHelpers>();

        // Core Intents controller surface.
        services.AddSingleton<ListIntentsEndpoint>();
        services.AddSingleton<ListIntentEventsEndpoint>();
        services.AddSingleton<GetIntentEndpoint>();
        services.AddSingleton<CreateIntentEndpoint>();
        services.AddSingleton<SetIntentTagsEndpoint>();
        services.AddSingleton<ReplaceIntentTextEndpoint>();
        services.AddSingleton<SetIntentStatusEndpoint>();
        services.AddSingleton<MoveIntentEndpoint>();
        services.AddSingleton<DeleteIntentEndpoint>();
        services.AddSingleton<ListIntentVersionsEndpoint>();

        // Pin controller surface.
        services.AddSingleton<PinIntentEndpoint>();
        services.AddSingleton<UnpinIntentEndpoint>();
        services.AddSingleton<MovePinEndpoint>();

        // Link controller surface.
        services.AddSingleton<CreateIntentLinkEndpoint>();
        services.AddSingleton<DeleteIntentLinkEndpoint>();
        services.AddSingleton<ListIntentLinksEndpoint>();
        services.AddSingleton<GetIntentLinksSummaryEndpoint>();

        // Attachment controller surface.
        services.AddSingleton<ListIntentAttachmentsEndpoint>();
        services.AddSingleton<UploadIntentAttachmentEndpoint>();
        services.AddSingleton<DownloadIntentAttachmentEndpoint>();
        services.AddSingleton<DeleteIntentAttachmentEndpoint>();

        return services;
    }
}
