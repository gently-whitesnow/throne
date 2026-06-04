using System.Text.Json;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Encoding side of the terminal WebSocket contract
/// (<c>specs/contracts/realtime/websocket/terminal.yaml</c>). Parsing lives in
/// <see cref="ClientFrameParser"/>.
/// </summary>
internal static class TerminalFrames
{
    /// <summary>Maximum payload bytes the bridge accepts per frame. Guards against runaway inputs.</summary>
    public const int MaxPayloadBytes = 64 * 1024;

    /// <summary>Server-side dimension bounds (mirrors the yaml schema for `resize`).</summary>
    public const int MinDimension = 1;
    public const int MaxDimension = 1000;

    public static string EncodeOutput(string data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "output");
            writer.WriteString("data", data);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }
}

internal readonly record struct ClientFrame(ClientFrameKind Kind, string? Data, int Cols, int Rows)
{
    public static ClientFrame Input(string data) => new(ClientFrameKind.Input, data, 0, 0);
    public static ClientFrame Resize(int cols, int rows) => new(ClientFrameKind.Resize, null, cols, rows);
}

internal enum ClientFrameKind
{
    Input,
    Resize,
}
