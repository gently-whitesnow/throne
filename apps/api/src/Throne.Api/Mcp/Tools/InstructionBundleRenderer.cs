using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.Instructions;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Готовит ответ `get_instruction_bundle` без дублирования полезной нагрузки.
///
/// Wire-policy (ADR-0003 §8.1, 2026-05 amendment): полный bundle уезжает только в
/// <see cref="TextContentBlock.Text"/>, wire <c>StructuredContent</c> = <c>null</c>.
/// Дубль payload в обе ветки прятал основной текст от structured-aware клиентов
/// (Claude Code) — incident intents 9cc71a8c… и 6e96cd22…. Compact refs (scope /
/// kind / instruction_id / current_version + missing_kinds) едут через audit OOB
/// envelope (<see cref="McpToolPayload.AuditSummary"/>) и попадают в
/// <c>mcp_call_log.result_summary</c>, не дублируясь по wire.
/// </summary>
internal static class InstructionBundleRenderer
{
    public static McpToolPayload Render(InstructionBundle bundle) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(bundle) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(bundle)));


    public static string RenderText(InstructionBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var sb = new StringBuilder(EstimateCapacity(bundle));
        sb.Append("mode=").Append(bundle.Mode);
        if (!string.IsNullOrEmpty(bundle.IntentId))
        {
            sb.Append(" intent_id=").Append(bundle.IntentId);
        }
        sb.Append('\n');

        foreach (var instruction in bundle.Instructions)
        {
            sb.Append("\n===== ")
              .Append(instruction.Scope).Append(':').Append(instruction.Kind)
              .Append(" (v").Append(instruction.CurrentVersion)
              .Append(", id=").Append(instruction.InstructionId)
              .Append(") =====\n\n");
            sb.Append(instruction.Text);
            if (!instruction.Text.EndsWith('\n'))
            {
                sb.Append('\n');
            }
        }

        if (bundle.MissingKinds.Count > 0)
        {
            sb.Append("\n[missing_kinds: ")
              .Append(string.Join(", ", bundle.MissingKinds))
              .Append("]\n");
        }

        return sb.ToString();
    }

    public static JsonObject RenderStructured(InstructionBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var refs = new JsonArray();
        foreach (var instruction in bundle.Instructions)
        {
            refs.Add(new JsonObject
            {
                ["scope"] = instruction.Scope,
                ["kind"] = instruction.Kind,
                ["instruction_id"] = instruction.InstructionId,
                ["current_version"] = instruction.CurrentVersion,
            });
        }

        var missing = new JsonArray();
        foreach (var kind in bundle.MissingKinds)
        {
            missing.Add(kind);
        }

        return new JsonObject
        {
            ["mode"] = bundle.Mode,
            ["intent_id"] = bundle.IntentId,
            ["instructions"] = refs,
            ["missing_kinds"] = missing,
        };
    }

    private static int EstimateCapacity(InstructionBundle bundle)
    {
        var total = 64;
        foreach (var instruction in bundle.Instructions)
        {
            total += 80 + instruction.Text.Length;
        }
        return total;
    }
}
