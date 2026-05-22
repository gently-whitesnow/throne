using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Compact-refs serialiser for the audit channel of prompt-like MCP tools.
///
/// Wire-policy (ADR-0003 §8.1, 2026-05 amendment): <see cref="ModelContextProtocol.Protocol.CallToolResult.StructuredContent"/>
/// for prompt-like tools is <c>null</c> on the wire. Compact refs (ids, versions,
/// counters, link/attachment manifests) flow only through the audit channel:
/// the renderer builds a <see cref="JsonObject"/> with the refs and converts it to
/// <see cref="IReadOnlyDictionary{TKey,TValue}"/> via <see cref="ToAuditSummary"/>;
/// the dictionary travels in <see cref="McpToolPayload.AuditSummary"/> and lands in
/// <c>mcp_call_log.result_summary</c> exactly as today (same shape as the previous
/// <see cref="Throne.Api.Mcp.McpResultSummarizer"/> deserialisation produced).
/// </summary>
internal static class McpStructuredContent
{
    private static readonly JsonSerializerOptions SerializerOptions = BuildOptions();

    public static IReadOnlyDictionary<string, object?> ToAuditSummary(JsonObject refs)
    {
        var json = refs.ToJsonString(SerializerOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, SerializerOptions)
               ?? new Dictionary<string, object?>();
    }

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.MakeReadOnly();
        return options;
    }
}
