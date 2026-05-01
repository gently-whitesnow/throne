using Throne.Api.Mcp;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneMcpCore(builder.Configuration);

builder.Services.AddControllers();

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
