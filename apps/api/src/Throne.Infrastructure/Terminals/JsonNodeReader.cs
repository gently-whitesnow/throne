using System.Text.Json;
using System.Text.Json.Nodes;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Helpers that fold JSON parse / value-cast exceptions into <c>bool</c> results so
/// callers (<see cref="ClientFrameParser"/>) can stay branch-light enough to fit the
/// per-type cyclomatic budget.
/// </summary>
internal static class JsonNodeReader
{
    public static JsonObject? TryParseObject(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }
        return ParseOrNull(payload) as JsonObject;
    }

    public static bool TryGetInt(JsonObject obj, string key, out int value)
    {
        value = 0;
        var node = obj[key];
        return node is not null && TryReadInt(node, out value);
    }

    public static string? StringOrNull(JsonObject obj, string key) =>
        obj[key]?.GetValue<string>();

    private static JsonNode? ParseOrNull(string payload)
    {
        try
        {
            return JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadInt(JsonNode node, out int value)
    {
        try
        {
            value = node.GetValue<int>();
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException)
        {
            value = 0;
            return false;
        }
    }
}
