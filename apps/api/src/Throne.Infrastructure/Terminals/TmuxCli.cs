using Microsoft.Extensions.Options;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Thin wrapper around <see cref="IProcessLauncher"/> for tmux subcommands. Mirrors
/// <c>GhCliInvoker</c> in the Git module: one place owns the executable path, the
/// timeout and the missing-binary classification. Higher-level callers
/// (<see cref="TmuxSessionManager"/>, <see cref="TmuxStreamBridge"/>) get a uniform
/// <see cref="ProcessRunResult"/> surface and never touch <c>System.Diagnostics</c>.
/// </summary>
internal sealed class TmuxCli(IProcessLauncher launcher, IOptions<TmuxOptions> options)
{
    private readonly TmuxOptions _opts = options.Value;

    public string ExecutablePath => _opts.ExecutablePath;

    public async Task<TmuxRunOutcome> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken ct,
        string? standardInput = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            var result = await launcher.RunAsync(
                new ProcessRunRequest(
                    FileName: _opts.ExecutablePath,
                    Arguments: arguments,
                    Timeout: _opts.CommandTimeout,
                    StandardInput: standardInput),
                ct);
            return TmuxRunOutcome.Completed(result);
        }
        catch (TimeoutException ex)
        {
            return TmuxRunOutcome.Failure($"tmux command timed out after {_opts.CommandTimeout.TotalSeconds:0.#}s: {ex.Message}");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            // The launcher could not even start the binary — tmux is not installed
            // or not on PATH. Folded into a "not available" outcome so callers can
            // map it to has-session=false / detected=false.
            return TmuxRunOutcome.BinaryMissing(ex.Message);
        }
    }
}

/// <summary>
/// Three-way outcome of a tmux shell-out:
/// <list type="bullet">
///   <item><see cref="Result"/> != null — process started and exited; inspect ExitCode.</item>
///   <item><see cref="BinaryMissingDetail"/> != null — <c>tmux</c> not on PATH.</item>
///   <item><see cref="FailureDetail"/> != null — timeout or other launcher-level failure.</item>
/// </list>
/// </summary>
internal readonly record struct TmuxRunOutcome(
    ProcessRunResult? Result,
    string? BinaryMissingDetail,
    string? FailureDetail)
{
    public bool IsAvailable => BinaryMissingDetail is null;
    public bool IsCompleted => Result is not null;
    public bool IsSuccess => Result is { ExitCode: 0 };

    public static TmuxRunOutcome Completed(ProcessRunResult result) => new(result, null, null);
    public static TmuxRunOutcome BinaryMissing(string detail) => new(null, detail, null);
    public static TmuxRunOutcome Failure(string detail) => new(null, null, detail);
}
