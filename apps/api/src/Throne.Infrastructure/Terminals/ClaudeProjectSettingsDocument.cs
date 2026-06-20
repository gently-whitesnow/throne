using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Throne.Infrastructure.Terminals;

internal static class ClaudeProjectSettingsDocument
{
    private const string EnabledServersKey = "enabledMcpjsonServers";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string? WithThroneMcpEnabled(string? existingJson)
    {
        var root = ParseRoot(existingJson);
        if (root is null)
        {
            return null;
        }

        if (root[EnabledServersKey] is not JsonArray enabled)
        {
            if (root.ContainsKey(EnabledServersKey))
            {
                return null;
            }
            enabled = [];
            root[EnabledServersKey] = enabled;
        }

        if (enabled.Any(IsThroneName))
        {
            return null;
        }

        enabled.Add(ThroneMcpServerConfig.Name);
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

    private static bool IsThroneName(JsonNode? node) =>
        node?.GetValueKind() == JsonValueKind.String
        && node.GetValue<string>() == ThroneMcpServerConfig.Name;
}
