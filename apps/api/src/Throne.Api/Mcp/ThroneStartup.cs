using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Throne.Api.Terminals;

namespace Throne.Api.Mcp;

public static class ThroneStartup
{
    /// <summary>
    /// Composite endpoint mapping for all custom Throne surfaces beyond MapControllers().
    /// Today: the embedded-terminal WebSocket bridge (T-03); future cross-cutting
    /// endpoints land here too. Also enables the WebSocket middleware required by the bridge.
    /// </summary>
    public static WebApplication MapThroneEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseWebSockets();
        app.MapThroneTerminalEndpoints();
        return app;
    }

    public static void AssertToolsRegistered(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var tools = services.GetServices<McpServerTool>().ToArray();

        if (tools.Length == 0)
        {
            throw new InvalidOperationException(
                "No MCP tools registered. Did you call AddThroneTools()?");
        }

        var unwrappedTools = tools.Where(t => t is not AuditingMcpServerTool).ToArray();
        if (unwrappedTools.Length > 0)
        {
            var names = string.Join(", ", unwrappedTools.Select(t => t.ProtocolTool.Name));
            throw new InvalidOperationException(
                $"Found {unwrappedTools.Length} MCP tool(s) registered without audit decorator: {names}. " +
                "All tools must be registered via AddThroneTool<T>().");
        }
    }
}
