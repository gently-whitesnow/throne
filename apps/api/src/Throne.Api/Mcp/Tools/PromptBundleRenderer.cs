using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.PromptParts;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Готовит ответ `get_prompt_bundle` без дублирования полезной нагрузки.
///
/// Wire-policy (ADR-0003 §8.1): полный bundle уезжает только в
/// <see cref="TextContentBlock.Text"/>, wire <c>StructuredContent</c> = <c>null</c>. Compact
/// refs (scope / key / prompt_part_id / current_version + missing_keys) едут через audit OOB
/// envelope и попадают в <c>mcp_call_log.result_summary</c>, не дублируясь по wire.
/// </summary>
internal static class PromptBundleRenderer
{
    public static McpToolPayload Render(PromptBundle bundle) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderText(bundle) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderStructured(bundle)));

    public static string RenderText(PromptBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var sb = new StringBuilder(EstimateCapacity(bundle));
        sb.Append("mode=").Append(bundle.Mode);
        if (!string.IsNullOrEmpty(bundle.IntentId))
        {
            sb.Append(" intent_id=").Append(bundle.IntentId);
        }
        sb.Append('\n');

        foreach (var part in bundle.Parts)
        {
            sb.Append("\n===== ")
              .Append(part.Scope).Append(':').Append(part.Key)
              .Append(" (v").Append(part.CurrentVersion)
              .Append(", id=").Append(part.PromptPartId)
              .Append(") =====\n\n");
            sb.Append(part.Text);
            if (!part.Text.EndsWith('\n'))
            {
                sb.Append('\n');
            }
        }

        if (bundle.MissingKeys.Count > 0)
        {
            sb.Append("\n[missing_keys: ")
              .Append(string.Join(", ", bundle.MissingKeys))
              .Append("]\n");
        }

        return sb.ToString();
    }

    public static JsonObject RenderStructured(PromptBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var refs = new JsonArray();
        foreach (var part in bundle.Parts)
        {
            refs.Add(new JsonObject
            {
                ["scope"] = part.Scope,
                ["key"] = part.Key,
                ["prompt_part_id"] = part.PromptPartId,
                ["current_version"] = part.CurrentVersion,
            });
        }

        var missing = new JsonArray();
        foreach (var key in bundle.MissingKeys)
        {
            missing.Add(key);
        }

        return new JsonObject
        {
            ["mode"] = bundle.Mode,
            ["intent_id"] = bundle.IntentId,
            ["parts"] = refs,
            ["missing_keys"] = missing,
        };
    }

    private static int EstimateCapacity(PromptBundle bundle)
    {
        var total = 64;
        foreach (var part in bundle.Parts)
        {
            total += 80 + part.Text.Length;
        }
        return total;
    }
}
