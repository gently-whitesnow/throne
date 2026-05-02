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

        var unwrappedTools = tools.Where(t => t is not AuditingMcpServerTool).ToArray();
        if (unwrappedTools.Length > 0)
        {
            var names = string.Join(", ", unwrappedTools.Select(t => t.ProtocolTool.Name));
            throw new InvalidOperationException(
                $"Found {unwrappedTools.Length} MCP tool(s) registered without audit decorator: {names}. " +
                "All tools must be registered via AddThroneTool<T>().");
        }

        var prompts = services.GetServices<McpServerPrompt>().ToArray();

        var unwrappedPrompts = prompts.Where(p => p is not AuditingMcpServerPrompt).ToArray();
        if (unwrappedPrompts.Length > 0)
        {
            var names = string.Join(", ", unwrappedPrompts.Select(p => p.ProtocolPrompt.Name));
            throw new InvalidOperationException(
                $"Found {unwrappedPrompts.Length} MCP prompt(s) registered without audit decorator: {names}. " +
                "All prompts must be registered via AddThronePrompt<T>().");
        }
    }
}
