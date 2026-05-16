using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Throne.Application.Events;

namespace Throne.Api.Realtime;

internal static class RealtimeServices
{
    public static IServiceCollection AddThroneRealtime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IRealtimeEventBroker, InMemoryRealtimeBroker>();
        services.AddSingleton<IntentLifecycleRealtimeMapper>();
        services.AddSingleton<IDomainEventHandler, RealtimeDomainEventHandler>();
        return services;
    }
}
