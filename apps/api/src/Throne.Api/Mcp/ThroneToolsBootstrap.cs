using Microsoft.Extensions.DependencyInjection;
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
        services.AddThroneTool<IntentTextTools>();
        services.AddThroneTool<IntentLinkTools>();
        services.AddThroneTool<IntentStatusTools>();
        services.AddThroneTool<IntentAttachmentTools>();
        services.AddThroneTool<InstructionPatchTools>();
        services.AddThroneTool<DreamTools>();

        return services;
    }
}
