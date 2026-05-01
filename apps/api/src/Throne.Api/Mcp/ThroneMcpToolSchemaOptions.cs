using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp;

/// <summary>
/// MCP clients (e.g. Claude + Zod) reject JSON Schema boolean <c>true</c> under <c>properties</c> — that is what
/// System.Text.Json emits for types with a custom <see cref="System.Text.Json.Serialization.JsonConverter"/> only
/// (see dotnet/runtime#115196). <see cref="IntentId"/> serializes as a string on the wire; we align the advertised
/// output schema accordingly.
/// </summary>
internal static class ThroneMcpToolSchemaOptions
{
    private static readonly AIJsonSchemaCreateOptions Shared = new()
    {
        TransformSchemaNode = FixIntentIdSchemaNode,
    };

    internal static McpServerToolCreateOptions ToolCreateOptions(IServiceProvider services) =>
        new() { Services = services, SchemaCreateOptions = Shared, };

    private static JsonNode FixIntentIdSchemaNode(AIJsonSchemaCreateContext context, JsonNode schema)
    {
        if (schema is JsonValue value &&
            value.TryGetValue(out bool allowsAnything) &&
            allowsAnything &&
            context.PropertyInfo is { } prop &&
            prop.PropertyType == typeof(IntentId))
        {
            return new JsonObject { ["type"] = "string" };
        }

        return schema;
    }
}
