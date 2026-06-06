using System.Text.Json.Nodes;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Parses inbound WebSocket text frames into <see cref="ClientFrame"/>. JSON access is
/// delegated to <see cref="JsonNodeReader"/> (ADR-0026 § 3 defers a codegen step until
/// a 3rd bidirectional channel materialises).
/// </summary>
internal static class ClientFrameParser
{
    public static bool TryParse(string payload, out ClientFrame frame)
    {
        frame = default;
        var obj = JsonNodeReader.TryParseObject(payload);
        if (obj is null)
        {
            return false;
        }

        var type = JsonNodeReader.StringOrNull(obj, "type");
        return type switch
        {
            "input" => TryParseInput(obj, out frame),
            "resize" => TryParseResize(obj, out frame),
            _ => false,
        };
    }

    private static bool TryParseInput(JsonObject obj, out ClientFrame frame)
    {
        frame = default;
        var data = JsonNodeReader.StringOrNull(obj, "data");
        if (data is null)
        {
            return false;
        }
        frame = ClientFrame.Input(data);
        return true;
    }

    private static bool TryParseResize(JsonObject obj, out ClientFrame frame)
    {
        frame = default;
        if (!JsonNodeReader.TryGetInt(obj, "cols", out var cols))
        {
            return false;
        }
        if (!JsonNodeReader.TryGetInt(obj, "rows", out var rows))
        {
            return false;
        }
        if (!IsValidDimension(cols) || !IsValidDimension(rows))
        {
            return false;
        }
        frame = ClientFrame.Resize(cols, rows);
        return true;
    }

    private static bool IsValidDimension(int value) =>
        value is >= TerminalFrames.MinDimension and <= TerminalFrames.MaxDimension;
}
