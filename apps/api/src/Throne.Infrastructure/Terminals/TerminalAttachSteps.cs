using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Individual steps used by <see cref="TerminalSessionAttach"/> — fifo creation,
/// pipe-pane wiring, scrollback flush. Pulled out so the attach orchestrator itself
/// stays inside the per-type cyclomatic budget.
/// </summary>
internal static class TerminalAttachSteps
{
    public static async Task<TmuxFifo?> TryCreateFifoAsync(
        IOptions<TmuxOptions> options,
        ILogger log,
        string intentId,
        string sessionName,
        ITerminalSocket connection,
        CancellationToken ct)
    {
        try
        {
            return await TmuxFifo.CreateAsync(options, intentId, ct);
        }
        catch (IOException ex)
        {
            TerminalsLog.BridgeFailed(log, sessionName, "fifo_create:" + ex.Message);
            await connection.CloseAsync(WebSocketCloseCodes.InternalError, "fifo_create_failed", ct);
            return null;
        }
    }

    public static async Task<bool> StartPipeAsync(
        TmuxCli tmux,
        ILogger log,
        string sessionName,
        TmuxFifo fifo,
        ITerminalSocket connection,
        CancellationToken ct)
    {
        var pipeResult = await tmux.RunAsync(
            ["pipe-pane", "-o", "-t", sessionName, $"cat >> {fifo.Path}"],
            ct);
        if (pipeResult.IsSuccess)
        {
            return true;
        }

        await fifo.DisposeAsync();
        var detail = TmuxOutcomeDetail.Extract(pipeResult);
        TerminalsLog.BridgeFailed(log, sessionName, "pipe_pane_failed:" + (detail ?? ""));
        await connection.CloseAsync(WebSocketCloseCodes.InternalError, "pipe_pane_failed", ct);
        return false;
    }

    public static async Task FlushScrollbackAsync(
        TmuxCli tmux,
        TmuxOptions options,
        string sessionName,
        ITerminalSocket connection,
        CancellationToken ct)
    {
        var lines = Math.Max(0, options.InitialScrollbackLines);
        var scrollback = await tmux.RunAsync(
            ["capture-pane", "-p", "-e", "-t", sessionName, "-S", "-" + lines.ToString(CultureInfo.InvariantCulture)],
            ct);
        if (scrollback.IsSuccess && !string.IsNullOrEmpty(scrollback.Result?.StandardOutput))
        {
            await connection.SendTextAsync(TerminalFrames.EncodeOutput(scrollback.Result!.StandardOutput), ct);
        }
    }
}
