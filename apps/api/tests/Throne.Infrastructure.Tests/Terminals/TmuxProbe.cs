using System.ComponentModel;
using System.Diagnostics;

namespace Throne.Infrastructure.Tests.Terminals;

/// <summary>
/// Cheap one-shot probe used by integration tests to early-return when the host has no
/// <c>tmux</c> binary on PATH. Kept as a standalone helper so the test class itself
/// stays inside the per-type cyclomatic budget.
/// </summary>
internal static class TmuxProbe
{
    public static bool IsAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("tmux", "-V")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (process is null)
            {
                return false;
            }
            process.WaitForExit(2000);
            return process.HasExited && process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }
}
