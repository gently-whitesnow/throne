using Throne.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneInfrastructure();
builder.Services
    .AddMcpServer()
    .WithHttpTransport();

var app = builder.Build();

app.MapMcp();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
