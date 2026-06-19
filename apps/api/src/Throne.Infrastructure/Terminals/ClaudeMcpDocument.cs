using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Throne.Infrastructure.Terminals;

internal static class ClaudeMcpDocument
{
    private const string McpServersKey = "mcpServers";
    private const string TypeKey = "type";
    private const string UrlKey = "url";
    private const string HttpType = "http";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? WithThroneServer(string? existingJson, string? apiBaseUrl)
    {
        var root = ParseRoot(existingJson);
        if (root is null)
        {
            return null;
        }

        if (root[McpServersKey] is not JsonObject servers)
        {
            if (root.ContainsKey(McpServersKey))
            {
                return null;
            }
            servers = [];
            root[McpServersKey] = servers;
        }

        var url = ThroneMcpServerConfig.Url(apiBaseUrl);
        if (IsCurrentServer(servers[ThroneMcpServerConfig.Name], url))
        {
            return null;
        }

        servers[ThroneMcpServerConfig.Name] = new JsonObject
        {
            [TypeKey] = HttpType,
            [UrlKey] = url,
        };
        return root.ToJsonString(SerializerOptions);
    }

    private static JsonObject? ParseRoot(string? existingJson)
    {
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(existingJson) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsCurrentServer(JsonNode? node, string url) =>
        node is JsonObject obj
        && StringValue(obj[TypeKey]) == HttpType
        && StringValue(obj[UrlKey]) == url;

    private static string? StringValue(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.String
            ? node.GetValue<string>()
            : null;
}
