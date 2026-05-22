using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders <see cref="IntentAttachmentTextSlice"/> for read_intent_attachment_text.
/// Decoded UTF-8 slice goes into <see cref="TextContentBlock.Text"/>; wire
/// <c>StructuredContent</c> is <c>null</c> (ADR-0003 §8.1, 2026-05 amendment).
/// Offset / truncation refs travel via the audit OOB envelope.
/// </summary>
internal static class AttachmentTextSliceRenderer
{
    public static McpToolPayload Render(IntentAttachmentTextSlice slice) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(slice) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(slice)));

    private static string RenderText(IntentAttachmentTextSlice s)
    {
        var sb = new StringBuilder(s.Text.Length + 192);
        sb.Append("content_type=").Append(s.ContentType)
          .Append(" total_size_bytes=").Append(s.TotalSizeBytes)
          .Append(" returned_bytes_start=").Append(s.ReturnedBytesStart)
          .Append(" returned_bytes_end=").Append(s.ReturnedBytesEnd)
          .Append(" truncated=").Append(s.Truncated ? "true" : "false")
          .Append("\n\n").Append(s.Text);
        if (s.Text.Length > 0 && !s.Text.EndsWith('\n'))
        {
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static JsonObject RenderStructured(IntentAttachmentTextSlice s) => new()
    {
        ["content_type"] = s.ContentType,
        ["total_size_bytes"] = s.TotalSizeBytes,
        ["returned_bytes_start"] = s.ReturnedBytesStart,
        ["returned_bytes_end"] = s.ReturnedBytesEnd,
        ["truncated"] = s.Truncated,
    };
}
