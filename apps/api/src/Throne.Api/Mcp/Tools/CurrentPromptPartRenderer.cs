using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.PromptPartPatches;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders <see cref="CurrentPromptPartView"/> for get_current_prompt_part. The part text goes
/// only into <see cref="TextContentBlock.Text"/>; wire <c>StructuredContent</c> is <c>null</c>
/// (ADR-0003 §8.1). Part id / scope / key / version refs travel via the audit OOB envelope.
/// </summary>
internal static class CurrentPromptPartRenderer
{
    public static McpToolPayload Render(CurrentPromptPartView view) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(view) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(view)));

    private static string RenderText(CurrentPromptPartView v)
    {
        var sb = new StringBuilder(v.Text.Length + 192);
        sb.Append("prompt_part_id=").Append(v.PromptPartId)
          .Append(" scope=").Append(v.Scope)
          .Append(" key=").Append(v.Key)
          .Append(" current_version=").Append(v.CurrentVersion)
          .Append(" updated_at=").Append(FormatTime(v.UpdatedAt))
          .Append("\n\n").Append(v.Text);
        if (v.Text.Length == 0 || !v.Text.EndsWith('\n'))
        {
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static JsonObject RenderStructured(CurrentPromptPartView v) => new()
    {
        ["prompt_part_id"] = v.PromptPartId,
        ["scope"] = v.Scope,
        ["key"] = v.Key,
        ["current_version"] = v.CurrentVersion,
        ["updated_at"] = FormatTime(v.UpdatedAt),
    };

    private static string FormatTime(DateTimeOffset dt) =>
        dt == DateTimeOffset.MinValue ? string.Empty : dt.ToString("O", CultureInfo.InvariantCulture);
}
