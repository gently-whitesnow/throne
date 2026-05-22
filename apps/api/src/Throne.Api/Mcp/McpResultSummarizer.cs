using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp;

/// <summary>
/// Builds <c>mcp_call_log.result_summary</c> from <see cref="CallToolResult.StructuredContent"/>.
///
/// Per ADR-0003 §8 (MCP wire-policy) tools that ship large prompt-like text put the
/// payload only in <see cref="TextContentBlock.Text"/>; <c>StructuredContent</c> is
/// already a compact refs JsonObject computed at the source. The summariser just
/// hands it to the audit sink — there is no per-tool re-parsing of the wire result.
/// </summary>
internal static class McpResultSummarizer
{
    private static readonly JsonSerializerOptions SummaryJsonOptions = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, object?>? Summarize(CallToolResult result)
    {
        if (result.StructuredContent is not { } structured)
        {
            return null;
        }

        var json = structured.GetRawText();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, SummaryJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
