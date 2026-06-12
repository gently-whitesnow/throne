using System.Text.Encodings.Web;
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
        // Encoder: эти опции SDK использует ТОЛЬКО для серилизации .NET-объектов в JSON-строку,
        // которую мы кладём в TextContentBlock.Text (см. McpToolResultConverter и IntentTools.GetIntent).
        // Wire-сериализацию envelope (JsonRpcMessage) SDK 1.2.0 хардкодит через свой
        // McpJsonUtilities.JsonContext с дефолтным JavaScriptEncoder — это сознательно, см. csharp-sdk#626.
        // Менять wire-encoder через рефлексию нельзя: csharp-sdk#64 показывает, что relaxed-режим
        // ломает отдельных MCP-клиентов на не-ASCII символах. Best-practice (PederHP в csharp-sdk#962):
        // для tool-ов с большим текстовым выходом не дублировать данные в Content + StructuredContent —
        // см. PromptBundleRenderer для get_prompt_bundle.
        //
        // UnsafeRelaxedJsonEscaping здесь оставляем для мелких объектов вроде get_intent: кириллица
        // в Intent.text без него экранируется как \uXXXX уже на первом проходе (×6 байт), плюс SDK
        // на wire экранирует ещё раз — на больших полях это заметно. Безопасно: получившаяся JSON-строка
        // всё равно проходит через SDK-овский encoder при отдаче и приводится к консервативному виду.
        var options = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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
