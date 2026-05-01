using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Throne.Api.Mcp;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(static options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddThroneMcpCore(builder.Configuration);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

var host = builder.Build();

ThroneStartup.AssertToolsRegistered(host.Services);

await host.RunAsync();
