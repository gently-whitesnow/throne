using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.Intents;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders <see cref="TextSlice"/> for read_intent_text. Body of the slice lives in
/// <see cref="TextContentBlock.Text"/>; wire <c>StructuredContent</c> is <c>null</c>
/// (ADR-0003 §8.1, 2026-05 amendment). Navigation metadata (versions, line bounds,
/// truncation) travels via the audit OOB envelope.
/// </summary>
internal static class TextSliceRenderer
{
    public static McpToolPayload Render(TextSlice slice) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(slice) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(slice)));

    private static string RenderText(TextSlice s)
    {
        var sb = new StringBuilder(s.Content.Length + 192);
        sb.Append("current_version=").Append(s.CurrentVersion)
          .Append(" start_line=").Append(s.StartLine)
          .Append(" end_line=").Append(s.EndLine)
          .Append(" total_lines=").Append(s.TotalLines)
          .Append(" truncated=").Append(s.Truncated ? "true" : "false");
        if (s.NextStartLine is { } next)
        {
            sb.Append(" next_start_line=").Append(next);
        }
        sb.Append("\n\n").Append(s.Content);
        if (s.Content.Length > 0 && !s.Content.EndsWith('\n'))
        {
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static JsonObject RenderStructured(TextSlice s) => new()
    {
        ["current_version"] = s.CurrentVersion,
        ["start_line"] = s.StartLine,
        ["end_line"] = s.EndLine,
        ["total_lines"] = s.TotalLines,
        ["truncated"] = s.Truncated,
        ["next_start_line"] = s.NextStartLine,
    };
}
