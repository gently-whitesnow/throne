using System.Text.Json;

namespace Throne.Api.Mcp;

/// <summary>
/// Некоторые MCP-харнессы (Anthropic remote-MCP relay в их числе) сериализуют
/// массив/объект аргументов как JSON-encoded строку, если в схеме параметра нет
/// явного <c>type</c>. Распаковываем такую строку в JsonElement(Array/Object),
/// чтобы downstream-биндер увидел реальный shape.
/// </summary>
internal static class McpJsonArgumentUnwrapper
{
    public static JsonElement Unwrap(JsonElement value, Type targetType)
    {
        if (value.ValueKind != JsonValueKind.String || IsStringLikeTarget(targetType))
        {
            return value;
        }

        var raw = value.GetString();
        if (string.IsNullOrEmpty(raw))
        {
            return value;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static bool IsStringLikeTarget(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying == typeof(string) || underlying.IsEnum;
    }
}
