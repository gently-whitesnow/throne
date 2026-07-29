namespace Throne.Api.Cli;

/// <summary>
/// <c>throne status</c>: running state, pid, url, version and home for the current
/// home's instance. Version is read live from the running process (<c>/version</c>)
/// so an instance left on an older binary is reported accurately; a stale pidfile is
/// cleaned up and reported as stopped.
/// </summary>
internal static class StatusCommand
{
    public static async Task<int> RunAsync(CliRequest request, CancellationToken ct)
    {
        var home = request.Home;
        Console.WriteLine($"home:    {home.Directory}");

        var state = DaemonState.TryRead(home);
        if (state is null || !state.IsAlive)
        {
            if (state is not null)
            {
                DaemonState.Clear(home);
            }

            Console.WriteLine("status:  stopped");
            if (OperatingSystem.IsWindows())
            {
                Console.WriteLine("note:    detached daemon is not supported on Windows yet (foreground only).");
            }

            return 0;
        }

        var version = await HealthProbe.TryGetVersionAsync(state.Url, ct) ?? Display(state.Version);
        Console.WriteLine("status:  running");
        Console.WriteLine($"pid:     {state.Pid}");
        Console.WriteLine($"url:     {Display(state.Url)}");
        Console.WriteLine($"version: {version}");
        Console.WriteLine($"uptime:  {UptimeFormat.Describe(state.StartedAt, DateTimeOffset.UtcNow)}");
        return 0;
    }

    private static string Display(string value) => string.IsNullOrEmpty(value) ? "unknown" : value;
}
