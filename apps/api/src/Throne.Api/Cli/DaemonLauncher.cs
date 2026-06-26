using System.Diagnostics;

namespace Throne.Api.Cli;

/// <summary>
/// Spawns the detached <c>throne serve</c> child, records its pid/state under the
/// home, then waits on <c>/health</c> so the success line (and browser) only fire
/// once the UI is actually serving. If the child dies during startup the tail of
/// its log is surfaced and the stale state cleaned up.
/// </summary>
internal static class DaemonLauncher
{
    public static async Task<int> StartAsync(CliRequest request, CancellationToken ct)
    {
        var home = request.Home;
        home.EnsureDirectory();
        DaemonLog.RotateIfLarge(home);

        var binary = Environment.ProcessPath!;
        var child = Process.Start(BuildStartInfo(binary, request));
        if (child is null)
        {
            Console.Error.WriteLine("Failed to start throne: could not spawn the daemon process.");
            return 1;
        }

        DaemonState.Write(home, new DaemonState(
            child.Id, request.Url, ThroneVersion.Current, DateTimeOffset.UtcNow, request.HostArgs));
        Console.WriteLine($"Starting throne at {request.Url} …");

        var healthy = await HealthProbe.WaitHealthyAsync(
            request.Url, TimeSpan.FromSeconds(20), () => !child.HasExited, ct);

        if (!healthy)
        {
            return ReportNotHealthy(request, child);
        }

        Console.WriteLine($"Throne running at {request.Url}  (pid {child.Id})");
        Console.WriteLine($"  home: {home.Directory}");
        Console.WriteLine("  stop with: throne stop   logs: throne logs -f");

        if (BrowserOpener.ShouldOpen(request.NoBrowser))
        {
            BrowserOpener.TryOpen(request.Url);
        }

        return 0;
    }

    private static int ReportNotHealthy(CliRequest request, Process child)
    {
        if (!child.HasExited)
        {
            Console.WriteLine($"throne is still starting at {request.Url} (pid {child.Id}).");
            Console.WriteLine("  follow startup with: throne logs -f");
            return 0;
        }

        DaemonState.Clear(request.Home);
        Console.Error.WriteLine($"throne exited during startup (code {child.ExitCode}). Recent log:");
        foreach (var line in LogReader.LastLines(request.Home.LogFile, 20))
        {
            Console.Error.WriteLine($"  {line}");
        }

        return 1;
    }

    // Spawn through /bin/sh so the child's stdout/stderr are redirected to the log (and
    // stdin to /dev/null) by the shell *before* exec: the launcher's inherited pipe is
    // released, so `out=$(throne …)` / `throne … | …` get EOF instead of blocking on the
    // long-lived daemon. `exec` preserves the pid, so child.Id is the throne process.
    private static ProcessStartInfo BuildStartInfo(string binary, CliRequest request)
    {
        var info = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("exec \"$@\" >>\"$THRONE_DAEMON_LOG\" 2>&1 </dev/null");
        info.ArgumentList.Add("throne");
        info.ArgumentList.Add(binary);
        info.ArgumentList.Add("serve");
        foreach (var arg in request.HostArgs)
        {
            info.ArgumentList.Add(arg);
        }

        info.Environment[DaemonRuntime.LogEnvVar] = request.Home.LogFile;
        info.Environment["THRONE_HOME"] = request.Home.Directory;
        return info;
    }
}
