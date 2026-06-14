using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Intents;
using Throne.Api.Mcp;
using Throne.Api.Shared;
using Throne.Application.Mcp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneMcpCore(builder.Configuration);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddControllers(o =>
    o.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider())
);

builder
    .Services.AddMcpServer(o => o.ServerInstructions = ThroneServerInstructions.MiniRouter)
    .WithHttpTransport();

var app = builder.Build();

ThroneStartup.AssertToolsRegistered(app.Services);

app.UseExceptionHandler(_ => { });
app.UseMiddleware<McpKeepAliveMiddleware>();

app.MapControllers();
app.MapThroneEndpoints();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
