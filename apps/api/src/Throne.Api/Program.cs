using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Auth;
using Throne.Api.Intents;
using Throne.Api.Mcp;
using Throne.Api.Mcp.Resources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneMcpCore(builder.Configuration);

builder.Services.AddControllers(o => o.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider()));

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithListResourcesHandler(static (request, ct) =>
        request.Services!.GetRequiredService<IntentAttachmentsResources>().ListAsync(request, ct))
    .WithReadResourceHandler(static (request, ct) =>
        request.Services!.GetRequiredService<IntentAttachmentsResources>().ReadAsync(request, ct));

var app = builder.Build();

ThroneStartup.AssertToolsRegistered(app.Services);

app.UseAuthentication();
app.UseAuthorization();

app.UsePersonalAccessTokenMcpAuth("/mcp");

app.MapControllers();
app.MapMcp("/mcp").AllowAnonymous();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program;
