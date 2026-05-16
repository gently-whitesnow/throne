using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using Throne.Mcp.Stdio;

// Throne.Mcp.Stdio is a thin STDIO→HTTP MCP proxy. The whole process exists to give Claude
// Code (and any other STDIO-only MCP host) a local entry point that forwards every tool call
// to the long-running Throne.Api at <Throne:ApiBaseUrl>. All writes — and therefore all
// domain events / SSE fan-out — happen in Throne.Api's process, so the web UI (also
// subscribed to that same process' in-memory broker) updates by construction. See ADR-0009.
//
// The upstream is wrapped in ResilientUpstreamClient which transparently reconnects after an
// API redeploy (no `/mcp reconnect` and no Claude restart needed).

const string DefaultApiBaseUrl = "http://localhost:5008";
const int ColdStartMaxAttempts = 30;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(static options =>
{
    // Only Information and above on stderr — reconnect retries are noisy at Debug/Trace
    // and would flood Claude Code's MCP log pane.
    options.LogToStandardErrorThreshold = LogLevel.Information;
});

var apiBaseUrl = builder.Configuration["Throne:ApiBaseUrl"]
    ?? Environment.GetEnvironmentVariable("THRONE_API_BASE_URL")
    ?? DefaultApiBaseUrl;

using var bootstrapLoggerFactory = LoggerFactory.Create(b =>
    b.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Information));
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Throne.Mcp.Stdio");

var upstreamUri = new Uri(new Uri(apiBaseUrl), "/mcp");

var transportOptions = new HttpClientTransportOptions
{
    Endpoint = upstreamUri,
    Name = "throne-upstream",
    MaxReconnectionAttempts = int.MaxValue,
    DefaultReconnectionInterval = TimeSpan.FromSeconds(1),
    ConnectionTimeout = TimeSpan.FromSeconds(15),
};

var upstream = new ResilientUpstreamClient(
    connectAsync: async ct =>
    {
        var transport = new HttpClientTransport(transportOptions, bootstrapLoggerFactory);
        return await McpClient.CreateAsync(
            transport,
            clientOptions: null,
            loggerFactory: bootstrapLoggerFactory,
            cancellationToken: ct);
    },
    log: bootstrapLoggerFactory.CreateLogger<ResilientUpstreamClient>());

try
{
    await upstream.ConnectWithBackoffAsync(ColdStartMaxAttempts, CancellationToken.None);
}
catch (Exception ex)
{
    StdioProxyLog.UpstreamConnectFailed(bootstrapLogger, upstreamUri, ex);
    await upstream.DisposeAsync();
    return 1;
}

StdioProxyLog.ProxyReady(bootstrapLogger, upstreamUri, upstream.CurrentTools.Count);

builder.Services
    .AddStdioProxy(upstream)
    .WithStdioServerTransport();

var host = builder.Build();
try
{
    await host.RunAsync();
}
finally
{
    await upstream.DisposeAsync();
}
return 0;
