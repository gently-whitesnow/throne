namespace Throne.Api.Cli;

/// <summary>
/// Bare <c>throne</c> and <c>throne -a</c>. Refuses to double-start (an alive
/// instance for this home is reported, not duplicated), then either detaches a
/// daemon (unix, published binary) or runs the host in the foreground (<c>-a</c>,
/// Windows, or <c>dotnet run</c>). Foreground registers its own pid/state so
/// <c>status</c>/<c>stop</c> from another terminal still work, and clears it on exit.
/// </summary>
internal static class StartCommand
{
    public static async Task<int> RunAsync(CliRequest request, CancellationToken ct)
    {
        if (request.PortError() is { } portError)
        {
            Console.Error.WriteLine($"throne: {portError}");
            return 1;
        }

        var home = request.Home;
        home.EnsureDirectory();

        var existing = DaemonState.TryRead(home);
        if (existing is { IsAlive: true })
        {
            Console.WriteLine($"throne is already running at {existing.Url} (pid {existing.Pid}).");
            Console.WriteLine($"  home: {home.Directory}   stop with: throne stop");
            return 0;
        }

        if (existing is not null)
        {
            DaemonState.Clear(home);
        }

        return request.Attach || !DaemonSupport.CanDaemonize()
            ? await RunForegroundAsync(request)
            : await DaemonLauncher.StartAsync(request, ct);
    }

    private static async Task<int> RunForegroundAsync(CliRequest request)
    {
        PrintForegroundBanner(request);

        var home = request.Home;
        DaemonState.Write(home, new DaemonState(
            Environment.ProcessId, request.Url, ThroneVersion.Current, DateTimeOffset.UtcNow));

        try
        {
            await ThroneWebHost.RunAsync(request.HostArgs, () => OnStarted(request));
        }
        finally
        {
            DaemonState.Clear(home);
        }

        return 0;
    }

    private static void PrintForegroundBanner(CliRequest request)
    {
        if (!request.Attach && OperatingSystem.IsWindows())
        {
            Console.WriteLine("Detached mode is not supported on Windows yet — running in the foreground.");
        }
        else if (!request.Attach)
        {
            Console.WriteLine("Running in the foreground (detached daemon needs the published binary).");
        }

        Console.WriteLine($"Starting throne at {request.Url} (Ctrl-C to stop) …");
    }

    private static void OnStarted(CliRequest request)
    {
        Console.WriteLine($"Throne running at {request.Url}");
        if (BrowserOpener.ShouldOpen(request.NoBrowser))
        {
            BrowserOpener.TryOpen(request.Url);
        }
    }
}
