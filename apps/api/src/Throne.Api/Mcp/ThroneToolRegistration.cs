using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.AI;
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
                var target = sp.GetRequiredService<TTool>();
                var mcpOptions = ThroneMcpToolSchemaOptions.ToolCreateOptions(sp);

                // SDK-built tool — used ТОЛЬКО для ProtocolTool (схема, name, аннотации).
                // Его InvokeAsync мы намеренно не вызываем: SDK глотает любые исключения внутри
                // AIFunction.InvokeAsync и возвращает generic "An error occurred invoking 'X'.",
                // теряя ApiException.Code / Detail / Extensions.
                var sdkTool = McpServerTool.Create(method, target, mcpOptions);

                // Отдельный AIFunction под нашим прямым контролем. Используем те же
                // schema/serializer-опции, чтобы binding параметров совпадал с тем,
                // что описано в ProtocolTool.InputSchema.
                var aiFunction = AIFunctionFactory.Create(method, target, new AIFunctionFactoryOptions
                {
                    Name = method.GetCustomAttribute<McpServerToolAttribute>()?.Name,
                    Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description,
                    MarshalResult = static (result, _, _) => new ValueTask<object?>(result),
                    SerializerOptions = mcpOptions.SerializerOptions,
                    JsonSchemaCreateOptions = mcpOptions.SchemaCreateOptions,
                });

                return new AuditingMcpServerTool(
                    sdkTool,
                    aiFunction,
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
