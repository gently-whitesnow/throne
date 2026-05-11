using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;

namespace Throne.Mcp.Stdio;

/// <summary>
/// DI wiring for the STDIO→HTTP MCP proxy. Extracted from <c>Program.cs</c> so the
/// bootstrap can be exercised by unit tests without spawning a real STDIO process.
/// In particular the proxy MUST forward upstream <c>ServerInstructions</c>
/// (the Throne mini-router from ADR-0014) into its own <see cref="McpServerOptions"/>;
/// without that, STDIO-only clients lose the runtime instruction routing.
/// </summary>
public static class StdioProxyBootstrap
{
    public static IMcpServerBuilder AddStdioProxy(
        this IServiceCollection services,
        IMcpClient upstream,
        IEnumerable<McpClientTool> upstreamTools)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(upstream);
        ArgumentNullException.ThrowIfNull(upstreamTools);

        services.AddSingleton(upstream);

        foreach (var tool in upstreamTools)
        {
            var captured = tool;
            services.AddSingleton<McpServerTool>(_ => McpServerTool.Create(captured));
        }

        return services.AddMcpServer(o => o.ServerInstructions = upstream.ServerInstructions);
    }
}
