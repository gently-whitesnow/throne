using Throne.Api.Intents;
using Throne.Api.Mcp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneMcpCore(builder.Configuration);

builder.Services.AddControllers(o => o.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider()));

builder.Services
    .AddMcpServer()
    .WithHttpTransport();

var app = builder.Build();

ThroneStartup.AssertToolsRegistered(app.Services);

app.MapControllers();
app.MapMcp("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
