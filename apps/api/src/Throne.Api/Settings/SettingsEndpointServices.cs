using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Settings.Endpoints;

namespace Throne.Api.Settings;

internal static class SettingsEndpointServices
{
    public static IServiceCollection AddThroneSettingsEndpoints(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TaskTrackerConnectionsEndpoint>();
        services.AddSingleton<TaskTrackerBoardsEndpoint>();

        return services;
    }
}
