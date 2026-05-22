using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.InstructionPatches;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders <see cref="CurrentInstructionView"/> for get_current_instruction. The
/// instruction text goes only into <see cref="TextContentBlock.Text"/>; wire
/// <c>StructuredContent</c> is <c>null</c> (ADR-0003 §8.1, 2026-05 amendment).
/// Instruction id / kind / version refs travel via the audit OOB envelope.
/// </summary>
internal static class CurrentInstructionRenderer
{
    public static McpToolPayload Render(CurrentInstructionView view) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(view) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(view)));

    private static string RenderText(CurrentInstructionView v)
    {
        var sb = new StringBuilder(v.Text.Length + 192);
        sb.Append("instruction_id=").Append(v.InstructionId)
          .Append(" kind=").Append(v.Kind)
          .Append(" current_version=").Append(v.CurrentVersion)
          .Append(" updated_at=").Append(FormatTime(v.UpdatedAt))
          .Append("\n\n").Append(v.Text);
        if (v.Text.Length == 0 || !v.Text.EndsWith('\n'))
        {
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static JsonObject RenderStructured(CurrentInstructionView v) => new()
    {
        ["instruction_id"] = v.InstructionId,
        ["kind"] = v.Kind,
        ["current_version"] = v.CurrentVersion,
        ["updated_at"] = FormatTime(v.UpdatedAt),
    };

    private static string FormatTime(DateTimeOffset dt) =>
        dt == DateTimeOffset.MinValue ? string.Empty : dt.ToString("O", CultureInfo.InvariantCulture);
}
