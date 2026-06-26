namespace Throne.Api.Cli;

/// <summary>
/// The daemon's log file lives at <c>&lt;home&gt;/throne.log</c>. The child appends
/// to it (console redirected in <see cref="DaemonRuntime"/>); <c>logs</c> tails it.
/// Rotation is a single size-capped roll to <c>throne.log.1</c> on start — enough to
/// keep one run's log from growing unbounded without a log framework.
/// </summary>
internal static class DaemonLog
{
    private const long MaxBytes = 8 * 1024 * 1024;

    public static string PreviousFile(ThroneHome home) => home.LogFile + ".1";

    public static void RotateIfLarge(ThroneHome home)
    {
        try
        {
            var info = new FileInfo(home.LogFile);
            if (!info.Exists || info.Length < MaxBytes)
            {
                return;
            }

            var previous = PreviousFile(home);
            if (File.Exists(previous))
            {
                File.Delete(previous);
            }

            File.Move(home.LogFile, previous);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
