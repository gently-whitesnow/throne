using Microsoft.Extensions.DependencyInjection;
using Throne.Api.Auth;
using Throne.Api.Intents;
using Throne.Api.Mcp;
using Throne.Application.Instructions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddThroneMcpCore(builder.Configuration);

builder.Services.AddControllers(o => o.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider()));

builder.Services
    .AddMcpServer(o => o.ServerInstructions = ThroneServerInstructions.MiniRouter)
    .WithHttpTransport();

var app = builder.Build();

ThroneStartup.AssertToolsRegistered(app.Services);

app.UseAuthentication();
// PAT must resolve before UseAuthorization() so the FallbackPolicy
// (RequireAuthenticatedUser) sees the PAT-derived ClaimsPrincipal on REST
// endpoints like /api/v1/chat-uploads. /mcp keeps its own RFC 9728 401 gate.
app.UsePersonalAccessTokenAuth();
app.UseAuthorization();

app.UseMcpRequiresBearer("/mcp");

app.UseMiddleware<McpKeepAliveMiddleware>();

app.MapControllers();
app.MapThroneEndpoints();
app.MapMcp("/mcp").AllowAnonymous();
app.MapOAuthProtectedResource("/mcp");
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

app.Run();

public partial class Program;
