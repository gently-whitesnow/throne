using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Throne.Application.Auth;
using Throne.Application.Ports;

namespace Throne.Api.Mcp;

public static class ThroneToolRegistration
{
    public static IServiceCollection AddThroneTool<TTool>(this IServiceCollection services)
        where TTool : class
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TTool>();

        var toolMethods = typeof(TTool)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .ToArray();

        if (toolMethods.Length == 0)
        {
            throw new InvalidOperationException(
                $"Type '{typeof(TTool).FullName}' has no methods marked with [McpServerTool].");
        }

        foreach (var method in toolMethods)
        {
            services.AddSingleton<McpServerTool>(sp =>
            {
                var inner = McpServerTool.Create(
                    method,
                    sp.GetRequiredService<TTool>(),
                    ThroneMcpToolSchemaOptions.ToolCreateOptions(sp));

                return new AuditingMcpServerTool(
                    inner,
                    sp.GetRequiredService<IMcpCallLogSink>(),
                    sp.GetRequiredService<ICurrentUserAccessor>(),
                    sp.GetRequiredService<TimeProvider>(),
                    sp.GetRequiredService<ILogger<AuditingMcpServerTool>>(),
                    sp.GetRequiredService<ServerVersion>());
            });
        }

        return services;
    }
}
