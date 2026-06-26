using System.Runtime.InteropServices;

namespace Throne.Api.Cli;

/// <summary>
/// libc calls the BCL does not surface. <c>setsid</c> detaches the daemon child
/// into its own session so closing the launching terminal (SIGHUP) does not take it
/// down; <c>kill</c> sends a real SIGTERM for graceful shutdown (the BCL
/// <c>Process.Kill</c> is SIGKILL only). Unix-only — guarded by the callers.
/// DllImport (not LibraryImport) keeps the project free of <c>AllowUnsafeBlocks</c>.
/// </summary>
internal static class UnixNative
{
    public const int Sigterm = 15;
    public const int Sigkill = 9;

    [DllImport("libc", EntryPoint = "setsid", SetLastError = true)]
    public static extern int Setsid();

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    public static extern int Kill(int pid, int signal);
}
