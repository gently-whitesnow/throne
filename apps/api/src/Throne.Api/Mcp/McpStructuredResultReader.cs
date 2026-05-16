using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp;

internal static class McpStructuredResultReader
{
    public static string? TryReadString(CallToolResult result, string propertyName)
    {
        if (result.StructuredContent is not { } element || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
