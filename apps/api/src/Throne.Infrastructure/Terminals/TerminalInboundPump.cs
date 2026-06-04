using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Reads WebSocket frames from the client and forwards them to
/// <see cref="InboundFrameDispatcher"/>. Stops when the peer closes cleanly or
/// cancellation fires.
/// </summary>
internal sealed class TerminalInboundPump(TmuxCli tmux)
{
    private readonly InboundFrameDispatcher _dispatcher = new(tmux);

    public async Task RunAsync(ITerminalSocket connection, string sessionName, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var payload = await connection.ReceiveTextAsync(ct);
            if (payload is null)
            {
                return;
            }
            await HandlePayloadAsync(payload, sessionName, ct);
        }
    }

    private async Task HandlePayloadAsync(string payload, string sessionName, CancellationToken ct)
    {
        if (payload.Length > TerminalFrames.MaxPayloadBytes)
        {
            return;
        }
        if (!ClientFrameParser.TryParse(payload, out var frame))
        {
            return;
        }
        await _dispatcher.DispatchAsync(frame, sessionName, ct);
    }
}
