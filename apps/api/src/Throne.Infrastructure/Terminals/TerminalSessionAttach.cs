using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// First-leg of the bridge: create the per-connection FIFO, point
/// <c>tmux pipe-pane</c> at it and flush the existing pane scrollback into the
/// freshly opened WebSocket. Returns <c>null</c> when any step fails — the bridge
/// then aborts the connection with the close code already sent here.
/// </summary>
internal sealed class TerminalSessionAttach(TmuxCli tmux, IOptions<TmuxOptions> options, ILogger log)
{
    public async Task<TmuxFifo?> AttachAsync(
        string intentId,
        string sessionName,
        ITerminalSocket connection,
        CancellationToken ct)
    {
        var fifo = await TerminalAttachSteps.TryCreateFifoAsync(options, log, intentId, sessionName, connection, ct);
        if (fifo is null)
        {
            return null;
        }

        if (!await TerminalAttachSteps.StartPipeAsync(tmux, log, sessionName, fifo, connection, ct))
        {
            return null;
        }

        await TerminalAttachSteps.FlushScrollbackAsync(tmux, sessionName, connection, ct);
        TerminalsLog.BridgeAttached(log, sessionName, fifo.Path);
        return fifo;
    }
}
