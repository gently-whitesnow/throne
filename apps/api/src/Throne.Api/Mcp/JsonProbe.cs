using System.Text.Json;

namespace Throne.Api.Mcp;

internal static class JsonProbe
{
    public static string? String(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out var value);
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    public static int? Int(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out var value);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result) ? result : null;
    }

    public static IEnumerable<JsonElement> Array(JsonElement element, string propertyName)
    {
        element.TryGetProperty(propertyName, out var value);
        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];
    }

    public static string[] StringArray(JsonElement element, string propertyName)
    {
        var items = new List<string>();
        foreach (var item in Array(element, propertyName))
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                items.Add(item.GetString()!);
            }
        }
        return items.ToArray();
    }
}
