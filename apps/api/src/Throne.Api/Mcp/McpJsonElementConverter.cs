using System.Text.Json;

namespace Throne.Api.Mcp;

internal static class McpJsonElementConverter
{
    public static object? ToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => ReadNumber(element),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.GetRawText(),
    };

    private static object ReadNumber(JsonElement element) =>
        element.TryGetInt64(out var i) ? i : element.GetDouble();
}
