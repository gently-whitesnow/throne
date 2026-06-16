using Microsoft.Extensions.DependencyInjection;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

/// <summary>
/// Composition entry point for the embedded-terminal WebSocket endpoint. The REST
/// surface (`POST .../terminal/run`, `.../terminal/restart`) is owned by T-05; T-03
/// only wires up the attach-only WebSocket and its capability gate.
/// </summary>
public static class TerminalEndpointsRegistration
{
    public static IServiceCollection AddThroneTerminalEndpoints(this IServiceCollection services)
    {
        services.AddSingleton<TerminalWebSocketEndpoint>();
        services.AddSingleton<TerminalHookStatusAck>();
        services.AddSingleton<TerminalVendorCatalogMapper>();
        return services;
    }

    public static IEndpointRouteBuilder MapThroneTerminalEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.Map(
            TerminalWebSocketRoutes.IntentsTerminalWs,
            static (HttpContext context, string intent_id, TerminalWebSocketEndpoint endpoint) =>
                endpoint.HandleAsync(context, intent_id)
        );

        return endpoints;
    }
}
