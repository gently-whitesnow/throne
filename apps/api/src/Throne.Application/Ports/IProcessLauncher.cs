namespace Throne.Application.Ports;

/// <summary>
/// Test seam over <c>System.Diagnostics.Process</c>. Lives in Application so
/// git-provider implementations in Infrastructure can depend on the abstraction
/// (not the concrete <c>ProcessRunner</c>) and unit tests can swap it for a
/// stub without spawning real processes — see ADR-0024 § 3 (shell-out via
/// <c>ProcessRunner</c>).
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Run <paramref name="request"/> to completion (or until
    /// <see cref="ProcessRunRequest.Timeout"/> elapses) and return captured
    /// stdout / stderr / exit code. The contract:
    /// <list type="bullet">
    ///   <item>Never throws on non-zero exit — callers inspect <see cref="ProcessRunResult.ExitCode"/>.</item>
    ///   <item>Throws <see cref="OperationCanceledException"/> when the supplied <paramref name="ct"/> fires.</item>
    ///   <item>Throws <see cref="TimeoutException"/> when <see cref="ProcessRunRequest.Timeout"/> elapses
    ///         and the process is force-killed.</item>
    /// </list>
    /// </summary>
    Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken ct);
}

/// <summary>
/// Inputs for a single <see cref="IProcessLauncher.RunAsync"/> call. Inline
/// stdin is intentionally omitted — slice 1's <c>gh</c> calls never need it.
/// </summary>
/// <param name="FileName">Executable name or absolute path (e.g. <c>gh</c>).</param>
/// <param name="Arguments">
///   Pre-split argument vector. Already-escaped strings would invite shell-injection,
///   so the runner sets <c>UseShellExecute=false</c> and pushes args through
///   <c>ProcessStartInfo.ArgumentList</c>.
/// </param>
/// <param name="WorkingDirectory">Directory the process is spawned in, or <see langword="null"/> for default.</param>
/// <param name="Environment">
///   Additional env vars merged on top of the parent process env. The parent env is
///   inherited verbatim so <c>gh</c> picks up <c>GH_TOKEN</c> / <c>GH_HOST</c>
///   without Throne needing to know about them (see ADR-0024 § 3).
/// </param>
/// <param name="Timeout">
///   Hard wall-clock cap. The runner force-kills the process tree when it expires
///   and throws <see cref="TimeoutException"/>.
/// </param>
public sealed record ProcessRunRequest(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string?>? Environment = null,
    TimeSpan? Timeout = null);

/// <summary>
/// Result of a finished process invocation.
/// </summary>
/// <param name="ExitCode">Process exit code; non-zero is left for the caller to interpret.</param>
/// <param name="StandardOutput">UTF-8 stdout captured into a string.</param>
/// <param name="StandardError">UTF-8 stderr captured into a string.</param>
/// <param name="Elapsed">Wall-clock time the process ran.</param>
public sealed record ProcessRunResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Elapsed)
{
    public bool IsSuccess => ExitCode == 0;
}
