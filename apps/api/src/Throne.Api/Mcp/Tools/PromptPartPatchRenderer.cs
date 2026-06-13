using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using Throne.Application.PromptPartPatches;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// Renders PromptPartPatch payloads for the three patch-shaped tools: single patch detail
/// (get_prompt_part_patch), proposed patch (propose_prompt_part_patch), and paged list
/// (list_prompt_part_patches).
///
/// Wire-policy (ADR-0003 §8.1): every textual body lives only in
/// <see cref="TextContentBlock.Text"/>. Wire <c>StructuredContent</c> is <c>null</c>. Compact
/// refs travel via the audit OOB envelope.
/// </summary>
internal static class PromptPartPatchRenderer
{
    public static McpToolPayload RenderDetail(McpPromptPartPatchReadModel patch, PromptPartPatchView view) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderDetailText(patch, view) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderDetailStructured(patch, view)));

    public static McpToolPayload RenderProposed(McpPromptPartPatchReadModel patch) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderPatchText(patch) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(BuildPatchRefs(patch)));

    public static McpToolPayload RenderList(IReadOnlyList<McpPromptPartPatchReadModel> items, string? nextCursor) => new(
        Wire: new CallToolResult
        {
            Content = [new TextContentBlock { Text = RenderListText(items, nextCursor) }],
            StructuredContent = null,
            IsError = false,
        },
        AuditSummary: McpStructuredContent.ToAuditSummary(RenderListStructured(items, nextCursor)));

    private static string RenderDetailText(McpPromptPartPatchReadModel patch, PromptPartPatchView view)
    {
        var sb = new StringBuilder(2048);
        AppendPatchHeader(sb, patch);
        sb.Append("current_part_version=").Append(view.CurrentPartVersion).Append('\n');
        sb.Append("base_version_matches_current=").Append(view.BaseVersionMatchesCurrent ? "true" : "false").Append('\n');
        AppendPatchBodies(sb, patch);
        AppendSection(sb, $"current_part (v{view.CurrentPartVersion})", view.CurrentPartText);
        AppendSection(sb, $"base_part (v{patch.BaseVersion})", view.BasePartText);
        AppendRationaleAndReject(sb, patch);
        return sb.ToString();
    }

    private static string RenderPatchText(McpPromptPartPatchReadModel patch)
    {
        var sb = new StringBuilder(1024);
        AppendPatchHeader(sb, patch);
        AppendPatchBodies(sb, patch);
        AppendRationaleAndReject(sb, patch);
        return sb.ToString();
    }

    private static string RenderListText(IReadOnlyList<McpPromptPartPatchReadModel> items, string? nextCursor)
    {
        var sb = new StringBuilder(512 + (items.Count * 256));
        AppendListHeader(sb, items.Count, nextCursor);
        foreach (var item in items)
        {
            sb.Append("\n----- patch id=").Append(item.Id).Append(" -----\n");
            AppendPatchHeader(sb, item);
            AppendPatchBodies(sb, item);
            AppendRationaleAndReject(sb, item);
        }
        return sb.ToString();
    }

    private static void AppendListHeader(StringBuilder sb, int count, string? nextCursor)
    {
        sb.Append("count=").Append(count);
        if (nextCursor is not null)
        {
            sb.Append(" next_cursor=").Append(nextCursor);
        }
        sb.Append('\n');
    }

    private static void AppendPatchBodies(StringBuilder sb, McpPromptPartPatchReadModel patch)
    {
        AppendSection(sb, "patch_text", patch.PatchText);
        if (patch.AppliedText is { } applied)
        {
            AppendSection(sb, "applied_text", applied);
        }
    }

    private static void AppendRationaleAndReject(StringBuilder sb, McpPromptPartPatchReadModel patch)
    {
        AppendSection(sb, "rationale", patch.Rationale);
        if (patch.RejectComment is { } reject)
        {
            AppendSection(sb, "reject_comment", reject);
        }
    }

    private static JsonObject RenderDetailStructured(McpPromptPartPatchReadModel patch, PromptPartPatchView view) => new()
    {
        ["patch"] = BuildPatchRefs(patch),
        ["current_part_version"] = view.CurrentPartVersion,
        ["base_version_matches_current"] = view.BaseVersionMatchesCurrent,
    };

    private static JsonObject RenderListStructured(IReadOnlyList<McpPromptPartPatchReadModel> items, string? nextCursor)
    {
        var refs = new JsonArray();
        foreach (var item in items)
        {
            refs.Add(BuildPatchRefs(item));
        }
        return new JsonObject
        {
            ["items"] = refs,
            ["next_cursor"] = nextCursor,
        };
    }

    private static JsonObject BuildPatchRefs(McpPromptPartPatchReadModel patch) => new()
    {
        ["id"] = patch.Id,
        ["target_scope"] = patch.TargetScope,
        ["target_key"] = patch.TargetKey,
        ["status"] = patch.Status,
        ["base_version"] = patch.BaseVersion,
        ["applied_version"] = patch.AppliedVersion,
        ["evidence_card_ids"] = BuildEvidenceRefs(patch.EvidenceCardIds),
        ["created_at"] = FormatTime(patch.CreatedAt),
        ["updated_at"] = FormatTime(patch.UpdatedAt),
        ["decided_at"] = patch.DecidedAt.HasValue ? FormatTime(patch.DecidedAt.Value) : null,
    };

    private static JsonArray BuildEvidenceRefs(IReadOnlyList<string> evidence)
    {
        var arr = new JsonArray();
        foreach (var id in evidence)
        {
            arr.Add(id);
        }
        return arr;
    }

    private static void AppendPatchHeader(StringBuilder sb, McpPromptPartPatchReadModel patch)
    {
        sb.Append("patch_id=").Append(patch.Id).Append('\n');
        sb.Append("target_scope=").Append(patch.TargetScope).Append('\n');
        sb.Append("target_key=").Append(patch.TargetKey).Append('\n');
        sb.Append("status=").Append(patch.Status).Append('\n');
        sb.Append("base_version=").Append(patch.BaseVersion).Append('\n');
        sb.Append("applied_version=")
          .Append(patch.AppliedVersion?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
          .Append('\n');
        sb.Append("created_at=").Append(FormatTime(patch.CreatedAt)).Append('\n');
        sb.Append("updated_at=").Append(FormatTime(patch.UpdatedAt)).Append('\n');
        if (patch.DecidedAt is { } decided)
        {
            sb.Append("decided_at=").Append(FormatTime(decided)).Append('\n');
        }
        if (patch.EvidenceCardIds.Count > 0)
        {
            sb.Append("evidence_card_ids=").Append(string.Join(", ", patch.EvidenceCardIds)).Append('\n');
        }
    }

    private static void AppendSection(StringBuilder sb, string label, string body)
    {
        sb.Append("\n===== ").Append(label).Append(" =====\n\n").Append(body);
        if (body.Length == 0 || !body.EndsWith('\n'))
        {
            sb.Append('\n');
        }
    }

    private static string FormatTime(DateTimeOffset dt) =>
        dt.ToString("O", CultureInfo.InvariantCulture);
}
