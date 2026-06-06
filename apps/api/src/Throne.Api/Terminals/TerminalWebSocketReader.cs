using System.Net.WebSockets;
using System.Text;

namespace Throne.Api.Terminals;

/// <summary>
/// Reads one WebSocket text message into a string. Returns <c>null</c> on a clean
/// close; non-text frames are dropped — a buggy client cannot tear the bridge down
/// by spamming binary.
/// </summary>
internal static class TerminalWebSocketReader
{
    public static async Task<string?> ReceiveTextAsync(WebSocket socket, int bufferBytes, CancellationToken ct)
    {
        var buffer = new byte[bufferBytes];
        var sb = new StringBuilder();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            if (result.MessageType != WebSocketMessageType.Text)
            {
                // drop the chunk; keep reading until a Text frame arrives
                continue;
            }
            sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage)
            {
                return sb.ToString();
            }
        }
    }
}
