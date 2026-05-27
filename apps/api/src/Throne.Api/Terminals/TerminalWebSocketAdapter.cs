using System.Net.WebSockets;
using System.Text;
using Throne.Application.Terminals;

namespace Throne.Api.Terminals;

/// <summary>
/// Adapts ASP.NET's <see cref="WebSocket"/> to the application-layer
/// <see cref="ITerminalSocket"/> seam. Lives in Throne.Api so the bridge in
/// Infrastructure never has to depend on ASP.NET types — keeps the layer rules
/// from <c>LayerDependencyRulesTests</c> green.
/// </summary>
internal sealed class TerminalWebSocketAdapter(WebSocket socket) : ITerminalSocket
{
    private const int ReceiveBufferBytes = 8 * 1024;

    public Task<string?> ReceiveTextAsync(CancellationToken ct) =>
        TerminalWebSocketReader.ReceiveTextAsync(socket, ReceiveBufferBytes, ct);

    public Task SendTextAsync(string payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, ct);
    }

    public async Task CloseAsync(int closeCode, string? description, CancellationToken ct)
    {
        if (socket.State is WebSocketState.Closed or WebSocketState.Aborted)
        {
            return;
        }

        try
        {
            await socket.CloseAsync((WebSocketCloseStatus)closeCode, description ?? string.Empty, ct);
        }
        catch (WebSocketException)
        {
            // peer already gone — close is best-effort
        }
        catch (OperationCanceledException)
        {
            // shutdown path
        }
    }
}
