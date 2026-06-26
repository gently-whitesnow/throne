namespace Throne.Api.Cli;

/// <summary>
/// Runs inside the detached child before the host boots. The launcher marks the
/// child with <c>THRONE_DAEMON_LOG</c>; seeing it, the child detaches from the
/// controlling terminal (<c>setsid</c>) and redirects console output to the log
/// file. Redirection happens before the host is built so the console logger it
/// constructs captures the file writer. A direct <c>throne serve</c> (no marker)
/// is a no-op here and logs to the terminal as usual.
/// </summary>
internal static class DaemonRuntime
{
    public const string LogEnvVar = "THRONE_DAEMON_LOG";

    public static void BootstrapIfDaemon()
    {
        var logPath = Environment.GetEnvironmentVariable(LogEnvVar);
        if (string.IsNullOrEmpty(logPath))
        {
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            _ = UnixNative.Setsid();
        }

        var directory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        Console.SetOut(writer);
        Console.SetError(writer);
    }
}
