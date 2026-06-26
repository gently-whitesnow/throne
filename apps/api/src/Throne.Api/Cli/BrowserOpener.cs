using System.ComponentModel;
using System.Diagnostics;

namespace Throne.Api.Cli;

/// <summary>
/// Best-effort "open the UI in a browser" on start. Suppressed when the caller is
/// not an interactive human: a non-TTY stdout (the typical agent/pipe signature),
/// an explicit <c>--no-browser</c>/<c>THRONE_NO_BROWSER</c>, or a known CI
/// environment. Opening never fails the launch — the printed URL is the fallback.
/// </summary>
internal static class BrowserOpener
{
    private static readonly string[] CiVariables =
    [
        "CI", "CONTINUOUS_INTEGRATION", "BUILD_NUMBER", "GITHUB_ACTIONS", "GITLAB_CI",
        "BUILDKITE", "TEAMCITY_VERSION", "JENKINS_URL", "TF_BUILD", "CIRCLECI", "TRAVIS", "APPVEYOR",
    ];

    public static bool ShouldOpen(bool noBrowserFlag)
    {
        if (noBrowserFlag || IsSet("THRONE_NO_BROWSER"))
        {
            return false;
        }

        if (Console.IsOutputRedirected)
        {
            return false;
        }

        return !IsCi();
    }

    public static void TryOpen(string url)
    {
        try
        {
            using var _ = Process.Start(OpenInfo(url));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
        }
    }

    private static ProcessStartInfo OpenInfo(string url)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo(url) { UseShellExecute = true };
        }

        var opener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
        var info = new ProcessStartInfo(opener) { UseShellExecute = false };
        info.ArgumentList.Add(url);
        return info;
    }

    private static bool IsCi()
    {
        foreach (var name in CiVariables)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value)
                && !string.Equals(value, "0", StringComparison.Ordinal)
                && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSet(string name) =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));
}
