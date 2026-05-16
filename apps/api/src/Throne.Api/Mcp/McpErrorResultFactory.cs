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

    public static CallToolResult Internal(string toolName, string? detail = null)
    {
        // Surface the real exception message to the MCP client: this server is consumed by agents
        // that need to self-correct their tool calls. The generic "An error occurred invoking 'X'."
        // string previously hid binding/argument errors completely.
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"An error occurred invoking '{toolName}'."
            : $"An error occurred invoking '{toolName}': {detail}";
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
