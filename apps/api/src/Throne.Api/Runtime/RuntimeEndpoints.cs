namespace Throne.Api.Runtime;

public static class RuntimeEndpoints
{
    public static WebApplication MapThroneRuntimeEndpoints(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/runtime/instance", (IConfiguration configuration) =>
            Results.Ok(new RuntimeInstanceDto(RuntimeInstanceDetector.IsEphemeral(configuration))));

        app.MapPost(
            "/api/v1/runtime/shutdown",
            (IConfiguration configuration, IHostApplicationLifetime lifetime) =>
            {
                if (!RuntimeInstanceDetector.IsEphemeral(configuration))
                {
                    return Results.NotFound();
                }

                _ = StopSoonAsync(lifetime);
                return Results.NoContent();
            });

        return app;
    }

    private static async Task StopSoonAsync(IHostApplicationLifetime lifetime)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(100));
        lifetime.StopApplication();
    }
}

public sealed record RuntimeInstanceDto(bool Ephemeral);
