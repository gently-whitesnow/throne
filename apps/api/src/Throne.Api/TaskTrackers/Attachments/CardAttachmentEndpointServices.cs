using Microsoft.Extensions.DependencyInjection;
using Throne.Api.TaskTrackers.Attachments.Endpoints;

namespace Throne.Api.TaskTrackers.Attachments;

/// <summary>
/// DI registration for the card-attachment endpoint classes (ADR-0052). Parallels
/// <c>RepositoryEndpointServices</c>; wired from <c>ThroneApiCoreServices</c>. Singleton lifetime mirrors
/// the underlying <c>CardAttachmentService</c>.
/// </summary>
internal static class CardAttachmentEndpointServices
{
    public static IServiceCollection AddThroneCardAttachmentEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CardAttachmentDtoMapper>();
        services.AddSingleton<ListIntentCardAttachmentsEndpoint>();
        services.AddSingleton<AttachIntentCardAttachmentEndpoint>();
        services.AddSingleton<DetachIntentCardAttachmentEndpoint>();
        services.AddSingleton<RefreshIntentCardAttachmentEndpoint>();

        return services;
    }
}
