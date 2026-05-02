using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using Throne.Mcp.Stdio;

// Throne.Mcp.Stdio is a thin STDIO→HTTP MCP proxy. The whole process exists to
// give Claude Code (and any other STDIO-only MCP host) a local entry point that
// forwards every tool call to the long-running Throne.Api at <Throne:ApiBaseUrl>.
// All writes — and therefore all domain events / SSE fan-out — happen in
// Throne.Api's process, so the web UI (also subscribed to that same process'
// in-memory broker) updates by construction. See ADR-0009.

const string DefaultApiBaseUrl = "http://localhost:5008";

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(static options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

var apiBaseUrl = builder.Configuration["Throne:ApiBaseUrl"]
    ?? Environment.GetEnvironmentVariable("THRONE_API_BASE_URL")
    ?? DefaultApiBaseUrl;

using var bootstrapLoggerFactory = LoggerFactory.Create(b =>
    b.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace));
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Throne.Mcp.Stdio");

var upstreamUri = new Uri(new Uri(apiBaseUrl), "/mcp");
var transport = new SseClientTransport(
    new SseClientTransportOptions
    {
        Endpoint = upstreamUri,
        Name = "throne-upstream",
    },
    bootstrapLoggerFactory);

IMcpClient upstream;
try
{
    upstream = await McpClientFactory.CreateAsync(
        transport,
        clientOptions: null,
        loggerFactory: bootstrapLoggerFactory);
}
catch (Exception ex)
{
    StdioProxyLog.UpstreamConnectFailed(bootstrapLogger, upstreamUri, ex);
    return 1;
}

IList<McpClientTool> upstreamTools;
try
{
    upstreamTools = await upstream.ListToolsAsync();
}
catch (Exception ex)
{
    StdioProxyLog.UpstreamListToolsFailed(bootstrapLogger, upstreamUri, ex);
    return 1;
}

StdioProxyLog.ProxyReady(bootstrapLogger, upstreamUri, upstreamTools.Count);

builder.Services.AddSingleton(upstream);

foreach (var tool in upstreamTools)
{
    var captured = tool;
    builder.Services.AddSingleton<McpServerTool>(_ => McpServerTool.Create(captured));
}

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

var host = builder.Build();
await host.RunAsync();
return 0;
