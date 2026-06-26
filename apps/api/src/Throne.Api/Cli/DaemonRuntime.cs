namespace Throne.Api.Cli;

/// <summary>
/// Runs inside the detached child before the host boots. The launcher marks the
/// child with <c>THRONE_DAEMON_LOG</c> and redirects its stdout/stderr to that log
/// at the OS level via the spawning shell; the only thing left for the child is to
/// detach from the controlling terminal (<c>setsid</c>) so closing the launching
/// terminal (SIGHUP) does not take it down. A direct <c>throne serve</c> (no marker)
/// is a no-op here and logs to the terminal as usual.
/// </summary>
internal static class DaemonRuntime
{
    public const string LogEnvVar = "THRONE_DAEMON_LOG";

    public static void BootstrapIfDaemon()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LogEnvVar)))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            _ = UnixNative.Setsid();
        }
    }
}
