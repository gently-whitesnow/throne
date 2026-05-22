using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders <see cref="McpIntentReadResult"/> for the get_intent tool.
///
/// Wire-policy (ADR-0003 §8.1, 2026-05 amendment): the full <c>Intent.text</c> +
/// readable metadata go into <see cref="TextContentBlock.Text"/>; wire
/// <c>StructuredContent</c> is <c>null</c>. Compact refs travel via the audit OOB
/// envelope (<see cref="McpToolPayload.AuditSummary"/>) to <c>mcp_call_log.result_summary</c>.
/// </summary>
internal static class IntentReadResultRenderer
{
    public static McpToolPayload Render(McpIntentReadResult result) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(result) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(result)));

    private static string RenderText(McpIntentReadResult r)
    {
        var sb = new StringBuilder(EstimateCapacity(r));
        AppendHeader(sb, r);
        AppendTextSection(sb, r.Text);
        AppendAttachments(sb, r.Attachments);
        AppendLinks(sb, r.Links);
        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb, McpIntentReadResult r)
    {
        sb.Append("intent_id=").Append(r.Id).Append('\n');
        sb.Append("status=").Append(r.Status).Append('\n');
        sb.Append("current_version=").Append(r.CurrentVersion).Append('\n');
        sb.Append("sort_key=").Append(r.SortKey).Append('\n');
        if (r.Tags.Count > 0)
        {
            sb.Append("tags=").Append(string.Join(", ", r.Tags.Select(t => t.Name))).Append('\n');
        }
        sb.Append("created_at=").Append(FormatTime(r.CreatedAt)).Append('\n');
        sb.Append("updated_at=").Append(FormatTime(r.UpdatedAt)).Append('\n');
    }

    private static void AppendTextSection(StringBuilder sb, string text)
    {
        sb.Append("\n===== text =====\n\n").Append(text);
        if (text.Length == 0 || !text.EndsWith('\n'))
        {
            sb.Append('\n');
        }
    }

    private static void AppendAttachments(StringBuilder sb, IReadOnlyList<McpIntentAttachmentReadResult> attachments)
    {
        if (attachments.Count == 0)
        {
            return;
        }
        sb.Append("\n===== attachments (").Append(attachments.Count).Append(") =====\n");
        foreach (var a in attachments)
        {
            sb.Append("- id=").Append(a.Id)
              .Append(" kind=").Append(a.Kind)
              .Append(" file_name=").Append(a.FileName)
              .Append(" content_type=").Append(a.ContentType)
              .Append(" size=").Append(a.SizeBytes)
              .Append(" recommended_tool=").Append(a.RecommendedTool ?? "<unsupported>")
              .Append('\n');
        }
    }

    private static void AppendLinks(StringBuilder sb, IReadOnlyList<McpIntentLinkRead> links)
    {
        if (links.Count == 0)
        {
            return;
        }
        sb.Append("\n===== links (").Append(links.Count).Append(") =====\n");
        foreach (var l in links)
        {
            sb.Append("- direction=").Append(l.Direction)
              .Append(" type=").Append(l.Type)
              .Append(" peer_id=").Append(l.Peer.Id)
              .Append(" peer_status=").Append(l.Peer.Status)
              .Append(" peer_version=").Append(l.Peer.CurrentVersion)
              .Append(" preview=").Append(l.Peer.Preview)
              .Append('\n');
        }
    }

    private static JsonObject RenderStructured(McpIntentReadResult r) => new()
    {
        ["id"] = r.Id,
        ["status"] = r.Status,
        ["current_version"] = r.CurrentVersion,
        ["sort_key"] = r.SortKey,
        ["tags"] = BuildTagRefs(r.Tags),
        ["created_at"] = FormatTime(r.CreatedAt),
        ["updated_at"] = FormatTime(r.UpdatedAt),
        ["attachments"] = BuildAttachmentRefs(r.Attachments),
        ["links"] = BuildLinkRefs(r.Links),
    };

    private static JsonArray BuildTagRefs(IReadOnlyList<McpTagRef> tags)
    {
        var arr = new JsonArray();
        foreach (var t in tags)
        {
            arr.Add(new JsonObject { ["id"] = t.Id, ["name"] = t.Name });
        }
        return arr;
    }

    private static JsonArray BuildAttachmentRefs(IReadOnlyList<McpIntentAttachmentReadResult> attachments)
    {
        var arr = new JsonArray();
        foreach (var a in attachments)
        {
            arr.Add(new JsonObject
            {
                ["id"] = a.Id,
                ["kind"] = a.Kind,
                ["content_type"] = a.ContentType,
                ["size_bytes"] = a.SizeBytes,
                ["recommended_tool"] = a.RecommendedTool,
            });
        }
        return arr;
    }

    private static JsonArray BuildLinkRefs(IReadOnlyList<McpIntentLinkRead> links)
    {
        var arr = new JsonArray();
        foreach (var l in links)
        {
            arr.Add(new JsonObject
            {
                ["id"] = l.Id,
                ["direction"] = l.Direction,
                ["type"] = l.Type,
                ["peer_id"] = l.Peer.Id,
                ["peer_status"] = l.Peer.Status,
                ["peer_current_version"] = l.Peer.CurrentVersion,
            });
        }
        return arr;
    }

    private static int EstimateCapacity(McpIntentReadResult r) =>
        256 + r.Text.Length + (r.Attachments.Count * 192) + (r.Links.Count * 192);

    private static string FormatTime(DateTimeOffset dt) =>
        dt.ToString("O", CultureInfo.InvariantCulture);
}
