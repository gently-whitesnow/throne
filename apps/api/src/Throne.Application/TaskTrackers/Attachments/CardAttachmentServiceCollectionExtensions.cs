using Microsoft.Extensions.DependencyInjection;
using Throne.Application.TaskTrackers.Attachments;

namespace Throne.Application;

/// <summary>
/// DI wiring for the card-attachment slice (ADR-0052): attach/detach/refresh/list of task-tracker cards
/// as read-only intent context. Namespaced <c>Throne.Application</c> (mirrors
/// <c>TaskTrackerServiceCollectionExtensions</c>) so <c>DependencyInjection</c> calls it without a new
/// using. The store port is registered in Infrastructure's <c>EfCoreModule</c>.
/// </summary>
public static class CardAttachmentServiceCollectionExtensions
{
    public static IServiceCollection AddCardAttachments(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<CardAttachmentResolver>();
        services.AddSingleton<CardAttachmentService>();

        return services;
    }
}
