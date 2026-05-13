using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Throne.Domain.Intents;

namespace Throne.Api.Mcp;

/// <summary>
/// MCP clients (e.g. Claude + Zod) reject JSON Schema boolean <c>true</c> under <c>properties</c> — that is what
/// System.Text.Json emits for types with a custom <see cref="System.Text.Json.Serialization.JsonConverter"/> only
/// (see dotnet/runtime#115196). <see cref="IntentId"/> serializes as a string on the wire; we align the advertised
/// output schema accordingly.
///
/// Также форсим <see cref="JsonIgnoreCondition.Never"/> для StructuredContent: Anthropic remote-MCP relay
/// валидирует ответ по auto-сгенерированной outputSchema, где nullable-поля помечены required. Если STJ
/// при сериализации опустит null-поле (`nextCursor`, `applied_text`, `decided_at` и т.п.), релэй заменяет
/// ответ на HTTP 500 у клиента. Явно выписываем `null` — schema совпадает, ответ доходит.
/// </summary>
internal static class ThroneMcpToolSchemaOptions
{
    private static readonly AIJsonSchemaCreateOptions Shared = new()
    {
        TransformSchemaNode = FixIntentIdSchemaNode,
    };

    private static readonly JsonSerializerOptions SerializerOptions = BuildSerializerOptions();

    internal static McpServerToolCreateOptions ToolCreateOptions(IServiceProvider services) =>
        new()
        {
            Services = services,
            SchemaCreateOptions = Shared,
            SerializerOptions = SerializerOptions,
        };

    private static JsonSerializerOptions BuildSerializerOptions()
    {
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.MakeReadOnly();
        return options;
    }

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
