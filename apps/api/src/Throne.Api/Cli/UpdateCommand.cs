using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Throne.Api.Cli;

/// <summary>
/// <c>throne update</c>: compares the running build against the latest GitHub
/// release for the current RID and, if newer, downloads and atomically swaps the
/// install in place. With <c>--restart</c> it re-execs the freshly installed
/// binary (a process cannot keep running its own replaced image).
/// </summary>
public static class UpdateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        var force = args.Contains("--force");
        var restart = args.Contains("--restart");

        var binaryPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(binaryPath)
            || string.Equals(Path.GetFileNameWithoutExtension(binaryPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine(
                "throne update works only from a published self-contained binary, not `dotnet run`.");
            return 1;
        }

        var installDir = AppContext.BaseDirectory;
        var rid = RuntimeInformation.RuntimeIdentifier;

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));

        ReleaseInfo release;
        try
        {
            release = await GitHubReleaseClient.GetLatestAsync(http, cts.Token);
        }
        catch (HttpRequestException ex)
        {
            Console.Error.WriteLine($"Failed to query latest release from {GitHubReleaseClient.Repository}: {ex.Message}");
            return 1;
        }

        var latest = Normalize(release.Tag);
        var current = Normalize(ThroneVersion.Current);

        if (!force && string.Equals(latest, current, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"throne is up to date ({ThroneVersion.Current}).");
            return 0;
        }

        var asset = release.FindAsset(rid);
        if (asset is null)
        {
            Console.Error.WriteLine($"Release {release.Tag} has no asset for runtime '{rid}'.");
            return 1;
        }

        Console.WriteLine($"Updating throne {ThroneVersion.Current} → {release.Tag} ({rid})…");
        try
        {
            await SelfUpdateInstaller.InstallAsync(http, asset, installDir, binaryPath, cts.Token);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or HttpRequestException)
        {
            Console.Error.WriteLine($"Update failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"Installed throne {release.Tag}.");

        if (restart)
        {
            return Restart(binaryPath);
        }

        Console.WriteLine("Restart throne to run the new version.");
        return 0;
    }

    private static int Restart(string binaryPath)
    {
        var psi = new ProcessStartInfo(binaryPath) { UseShellExecute = false };
        psi.ArgumentList.Add("serve");
        var process = Process.Start(psi);
        return process is null ? 1 : 0;
    }

    private static string Normalize(string version) => version.Trim().TrimStart('v', 'V');
}
