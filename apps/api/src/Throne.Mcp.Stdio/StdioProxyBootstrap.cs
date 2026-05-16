using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Throne.Mcp.Stdio;

/// <summary>
/// DI wiring for the STDIO→HTTP MCP proxy. The downstream MCP server exposes two dynamic
/// handlers (<c>tools/list</c>, <c>tools/call</c>) that read the live tool snapshot from
/// <see cref="ResilientUpstreamClient"/> on every request — so the snapshot can change at
/// any point (e.g. after an upstream redeploy) without rebuilding DI.
///
/// <see cref="McpServerOptions.ServerInstructions"/> is fixed at the first-connect value
/// (ADR-0014 mini-router). On divergence we exit and let the host re-spawn (see
/// <see cref="ToolsChangedRelay"/>).
/// </summary>
public static class StdioProxyBootstrap
{
    public static IMcpServerBuilder AddStdioProxy(
        this IServiceCollection services,
        ResilientUpstreamClient upstream)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(upstream);

        services.AddSingleton(upstream);
        services.AddSingleton(upstream.Connection.ToolsState);
        services.AddSingleton(upstream.Connection.InstructionsState);
        services.AddHostedService<ToolsChangedRelay>();

        return services
            .AddMcpServer(o => o.ServerInstructions = upstream.InitialServerInstructions)
            .WithListToolsHandler((_, _) =>
                ValueTask.FromResult(new ListToolsResult { Tools = [.. upstream.CurrentTools] }))
            .WithCallToolHandler((ctx, ct) =>
            {
                ArgumentNullException.ThrowIfNull(ctx.Params);
                return upstream.CallToolAsync(ctx.Params, ct);
            });
    }
}
