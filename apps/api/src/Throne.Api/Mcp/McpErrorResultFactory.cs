using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.Errors;

namespace Throne.Api.Mcp;

internal static class McpErrorResultFactory
{
    private static readonly JsonSerializerOptions ExtensionsJsonOptions = new(JsonSerializerDefaults.Web);

    public static CallToolResult FromApiException(ApiException ex)
    {
        var structured = new JsonObject
        {
            ["code"] = ex.Code,
            ["message"] = ex.Detail,
        };

        if (ex.Extensions.Count > 0)
        {
            structured["data"] = ExtensionsToJsonObject(ex.Extensions);
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = ex.Detail }],
            StructuredContent = ToElement(structured),
        };
    }

    public static CallToolResult Internal(string toolName)
    {
        var message = $"An error occurred invoking '{toolName}'.";
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = message }],
            StructuredContent = ToElement(new JsonObject
            {
                ["code"] = "internal_error",
                ["message"] = message,
            }),
        };
    }

    private static JsonElement ToElement(JsonNode node) =>
        JsonSerializer.SerializeToElement(node, ExtensionsJsonOptions);

    private static JsonObject ExtensionsToJsonObject(IReadOnlyDictionary<string, object?> extensions)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in extensions)
        {
            obj[key] = JsonSerializer.SerializeToNode(value, ExtensionsJsonOptions);
        }
        return obj;
    }
}
