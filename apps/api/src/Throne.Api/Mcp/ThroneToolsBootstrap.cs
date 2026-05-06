using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Mcp.Resources;
using Throne.Api.Mcp.Tools;

namespace Throne.Api.Mcp;

public static class ThroneToolsBootstrap
{
    public static IServiceCollection AddThroneTools(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new ServerVersion(
            typeof(ThroneToolsBootstrap).Assembly.GetName().Version?.ToString() ?? "0.0.0"));

        services.AddThroneTool<IntentTools>();
        services.AddThroneTool<DreamTools>();

        services.AddSingleton<IntentAttachmentsResources>();

        return services;
    }
}
