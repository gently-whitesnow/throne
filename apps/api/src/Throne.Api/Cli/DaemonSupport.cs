using System.Diagnostics;

namespace Throne.Api.Cli;

/// <summary>
/// Whether this process can spawn a detached daemon. Unix-only for now (Windows
/// detach/stop is a later best-effort slice), and only from a real published
/// <c>throne</c> binary — under <c>dotnet run</c> the executable is <c>dotnet</c>,
/// which cannot be re-spawned as a daemon, so the launcher falls back to foreground.
/// </summary>
internal static class DaemonSupport
{
    public static bool CanDaemonize()
    {
        if (OperatingSystem.IsWindows())
        {
            return false;
        }

        var path = Environment.ProcessPath;
        return !string.IsNullOrEmpty(path) && !IsDotnetHost(path);
    }

    public static bool IsDotnetHost(string processPath) =>
        string.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase);

    public static async Task TerminateAsync(int pid, TimeSpan grace, CancellationToken ct)
    {
        if (!DaemonState.IsProcessAlive(pid))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            ForceKill(pid);
            return;
        }

        _ = UnixNative.Kill(pid, UnixNative.Sigterm);

        var deadline = DateTimeOffset.UtcNow + grace;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!DaemonState.IsProcessAlive(pid))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
        }

        if (DaemonState.IsProcessAlive(pid))
        {
            _ = UnixNative.Kill(pid, UnixNative.Sigkill);
        }
    }

    private static void ForceKill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
        }
    }
}
