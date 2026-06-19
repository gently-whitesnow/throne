using System.Text.Json;
using System.Text.Json.Nodes;

namespace Throne.Infrastructure.Terminals;

internal static class OpencodeMcpServers
{
    private const string McpKey = "mcp";
    private const string TypeKey = "type";
    private const string UrlKey = "url";
    private const string EnabledKey = "enabled";
    private const string RemoteType = "remote";

    public static IReadOnlyDictionary<string, JsonNode> MergeThroneServer(
        string? existingJson,
        string? apiBaseUrl)
    {
        var servers = ExistingServers(existingJson);
        servers[ThroneMcpServerConfig.Name] = new JsonObject
        {
            [TypeKey] = RemoteType,
            [UrlKey] = ThroneMcpServerConfig.Url(apiBaseUrl),
            [EnabledKey] = true,
        };
        return servers;
    }

    private static Dictionary<string, JsonNode> ExistingServers(string? existingJson)
    {
        var result = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return result;
        }

        try
        {
            if (JsonNode.Parse(existingJson) is not JsonObject root
                || root[McpKey] is not JsonObject mcp)
            {
                return result;
            }

            foreach (var (name, server) in mcp)
            {
                if (server is not null && name != ThroneMcpServerConfig.Name)
                {
                    result[name] = server.DeepClone();
                }
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }
}
