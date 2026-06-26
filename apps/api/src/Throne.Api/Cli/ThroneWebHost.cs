using Microsoft.AspNetCore.Http.Features;
using Throne.Api.Hosting;
using Throne.Api.Intents;
using Throne.Api.Shared;

namespace Throne.Api.Cli;

/// <summary>
/// Boots the Kestrel host that serves the API, realtime/terminal surfaces and the
/// SPA. Static files + SPA fallback are wired after the API routes so they never
/// shadow <c>/api</c>, <c>/health</c>, <c>/version</c>, the SSE stream or the
/// terminal WebSocket; <see cref="Microsoft.AspNetCore.Builder.WebApplication"/>
/// matches those endpoints first and <c>MapFallbackToFile</c> only catches the
/// remaining client-router paths (e.g. <c>/login/</c>).
/// </summary>
public static class ThroneWebHost
{
    public static async Task RunAsync(string[] args, Action? onStarted = null)
    {
        // ContentRoot must be the binary's directory, not the launch CWD: the
        // single-file bundle ships wwwroot/ and appsettings.json next to the
        // executable, and `throne` is expected to run from anywhere on PATH.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Services.AddThroneApiCore(builder.Configuration);
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddControllers(o =>
            o.ModelBinderProviders.Insert(0, new FileParameterModelBinderProvider())
        );

        var app = builder.Build();

        app.UseExceptionHandler(_ => { });

        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapControllers();
        app.MapThroneEndpoints();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/version", () => Results.Ok(new { version = ThroneVersion.Current }));

        // More specific than the SPA file fallback, so an unmatched /api/* request
        // 404s as an API call instead of silently returning index.html.
        app.MapFallback("/api/{**slug}", () => Results.NotFound());
        app.MapFallbackToFile("index.html");

        if (onStarted is not null)
        {
            app.Lifetime.ApplicationStarted.Register(onStarted);
        }

        await app.RunAsync();
    }
}
