using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace Throne.Api.Mcp;

public static class ThroneStartup
{
    public static void AssertToolsRegistered(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var tools = services.GetServices<McpServerTool>().ToArray();

        if (tools.Length == 0)
        {
            throw new InvalidOperationException(
                "No MCP tools registered. Did you call AddThroneTools()?");
        }

        var unwrapped = tools.Where(t => t is not AuditingMcpServerTool).ToArray();
        if (unwrapped.Length > 0)
        {
            var names = string.Join(", ", unwrapped.Select(t => t.ProtocolTool.Name));
            throw new InvalidOperationException(
                $"Found {unwrapped.Length} MCP tool(s) registered without audit decorator: {names}. " +
                "All tools must be registered via AddThroneTool<T>().");
        }
    }
}
