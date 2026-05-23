using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Git;

/// <summary>
/// Single entry point for shell-out to vendor CLIs (<c>gh</c>, <c>glab</c>).
/// Owns: timeouts, capture of stdout/stderr, exit code, cancellation, env merge,
/// structured logging. Per ADR-0024 § 3 no provider implementation talks to
/// <see cref="Process"/> directly — they all go through this runner.
/// Construction helpers live in companion types so this class stays inside the
/// CA1502 type-level cyclomatic budget.
/// </summary>
internal sealed class ProcessRunner(ILogger<ProcessRunner> log) : IProcessLauncher
{
    public async Task<ProcessRunResult> RunAsync(ProcessRunRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FileName);

        using var process = new Process
        {
            StartInfo = ProcessStartInfoBuilder.Build(request),
            EnableRaisingEvents = true,
        };

        var collector = new ProcessOutputCollector();
        collector.Attach(process);

        ProcessRunnerLog.Starting(log, request.FileName, request.Arguments.Count, request.WorkingDirectory);

        var stopwatch = Stopwatch.StartNew();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await WaitForExitAsync(process, request, stopwatch, ct);

        stopwatch.Stop();
        ProcessRunnerLog.Finished(log, request.FileName, process.ExitCode, stopwatch.ElapsedMilliseconds);

        return new ProcessRunResult(
            ExitCode: process.ExitCode,
            StandardOutput: collector.StandardOutput,
            StandardError: collector.StandardError,
            Elapsed: stopwatch.Elapsed);
    }

    private async Task WaitForExitAsync(
        Process process,
        ProcessRunRequest request,
        Stopwatch stopwatch,
        CancellationToken ct)
    {
        using var timeoutCts = NewTimeoutSource(request.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            // Drain remaining async output before reading buffers.
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            HandleCancellation(process, request, stopwatch, timeoutCts, ct);
            throw;
        }
    }

    private void HandleCancellation(
        Process process,
        ProcessRunRequest request,
        Stopwatch stopwatch,
        CancellationTokenSource timeoutCts,
        CancellationToken ct)
    {
        KillTree(process);
        stopwatch.Stop();

        if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            ProcessRunnerLog.Timeout(log, request.FileName, stopwatch.ElapsedMilliseconds);
            throw new TimeoutException(
                $"Process '{request.FileName}' exceeded timeout of {request.Timeout!.Value.TotalMilliseconds}ms.");
        }
    }

    private static CancellationTokenSource NewTimeoutSource(TimeSpan? timeout) =>
        timeout is { } value ? new CancellationTokenSource(value) : new CancellationTokenSource();

    private static void KillTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited between HasExited check and Kill — ignore.
        }
    }
}
