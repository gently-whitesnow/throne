using Throne.Api.Mcp;
using Throne.Application;
using Throne.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneApplication();
builder.Services.AddThroneInfrastructure(builder.Configuration);
builder.Services.AddThroneTools();

builder.Services
    .AddMcpServer()
    .WithHttpTransport();

var app = builder.Build();

ThroneStartup.AssertToolsRegistered(app.Services);

app.MapMcp();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
