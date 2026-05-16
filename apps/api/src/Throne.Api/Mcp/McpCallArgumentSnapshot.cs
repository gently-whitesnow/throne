using System.Text.Json;

namespace Throne.Api.Mcp;

internal static class McpCallArgumentSnapshot
{
    public static Dictionary<string, object?> Normalize(IDictionary<string, JsonElement>? arguments)
    {
        if (arguments is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var dict = new Dictionary<string, object?>(arguments.Count, StringComparer.Ordinal);
        foreach (var (key, value) in arguments)
        {
            dict[key] = McpJsonElementConverter.ToObject(value);
        }
        return dict;
    }

    public static string? ExtractIntentId(IDictionary<string, JsonElement>? arguments) =>
        ReadStringProperty(arguments, "intent_id");

    public static string? ExtractModeHint(string toolName, IDictionary<string, JsonElement>? arguments) =>
        toolName == "get_instruction_bundle"
            ? ReadStringProperty(arguments, "mode")
            : null;

    private static string? ReadStringProperty(IDictionary<string, JsonElement>? arguments, string name) =>
        arguments is not null && arguments.TryGetValue(name, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
