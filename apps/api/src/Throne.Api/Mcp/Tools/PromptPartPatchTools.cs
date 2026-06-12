using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.PromptPartPatches;
using Throne.Domain.PromptParts;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// MCP surface for PromptPartPatch (ADR-0036 supersedes ADR-0021). Four tools:
///   * <c>propose_prompt_part_patch</c> — frontier creates a fresh proposed patch;
///   * <c>list_prompt_part_patches</c> — paginated list (newest first);
///   * <c>get_prompt_part_patch</c> — single patch with current target text;
///   * <c>get_current_prompt_part</c> — current text+version of a prompt part so the agent can
///     ground <c>base_version</c>.
///
/// Apply / edit / reject live on the HTTP surface — they are user actions, not agent actions.
///
/// Wire-policy (ADR-0003 §8): every textual body goes only into
/// <see cref="ModelContextProtocol.Protocol.TextContentBlock.Text"/>; StructuredContent carries
/// compact refs.
/// </summary>
[McpServerToolType]
public sealed class PromptPartPatchTools(
    ProposePromptPartPatchHandler proposeHandler,
    ListPromptPartPatchesHandler listHandler,
    GetPromptPartPatchHandler getHandler,
    GetCurrentPromptPartHandler currentHandler)
{
    [McpServerTool(Name = "propose_prompt_part_patch")]
    [Description("Propose a new PromptPartPatch in status 'proposed' for a prompt part identified by (target_scope, target_key). base_version must match the live PromptPart.current_version (use get_current_prompt_part to read it; for dream this is usually scope=\"user\", key in {work, interview}). evidence_card_ids are opaque agent-side references. Apply / reject is a user action via UI/HTTP — agents cannot decide their own proposals. Pass a unique idempotency_key per logical proposal so a transport-level retry returns the original patch instead of creating a duplicate.")]
    public async Task<McpToolPayload> ProposePromptPartPatch(
        [Description("Scope of the target prompt part: system | user (dream targets user).")] string target_scope,
        [Description("Key of the target prompt part (e.g. work | interview).")] string target_key,
        [Description("Whole new text of the target prompt part (the apply path replaces the text verbatim with this).")] string patch_text,
        [Description("Opaque agent-side evidence ids; stored verbatim on the patch for audit but not validated.")] IReadOnlyList<string> evidence_card_ids,
        [Description("Short rationale; ≤500 characters.")] string rationale,
        [Description("PromptPart.current_version the agent is editing on top of. Mismatch → 409 prompt_part_patch.needs_rebase.")] int base_version,
        [Description("Optional client-generated dedup key (≤64 chars). When present, the server returns the previously created patch on a retry with the same key instead of inserting a new one.")] string? idempotency_key = null,
        CancellationToken cancellationToken = default)
    {
        var patch = await proposeHandler.HandleAsync(
            new ProposePromptPartPatchCommand(
                target_scope,
                target_key,
                patch_text,
                evidence_card_ids ?? Array.Empty<string>(),
                rationale,
                base_version,
                idempotency_key),
            cancellationToken);
        return PromptPartPatchRenderer.RenderProposed(PromptPartPatchMcpMapper.ToReadModel(patch));
    }

    [McpServerTool(Name = "list_prompt_part_patches", ReadOnly = true)]
    [Description("List PromptPartPatches owned by the caller, ordered by created_at descending. Filter by target_scope, target_key and/or status. Pagination is opaque-cursor based. Useful for de-duplication: read past 'rejected' patches with reject_comment to avoid re-proposing the same rule. Empty `items` is a valid success state — do NOT treat it as an error.")]
    public async Task<McpToolPayload> ListPromptPartPatches(
        [Description("Optional target_scope filter: system | user.")] string? target_scope = null,
        [Description("Optional target_key filter (prompt part key).")] string? target_key = null,
        [Description("Optional status filter: proposed | applied | applied_edited | rejected | superseded.")] string? status = null,
        [Description("Page size, default 50, capped at 200.")] int? limit = null,
        [Description("Opaque cursor returned as next_cursor by the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await listHandler.HandleAsync(
                new ListPromptPartPatchesQuery(target_scope, target_key, status, limit, cursor),
                cancellationToken);
            var items = page.Items.Select(PromptPartPatchMcpMapper.ToReadModel).ToList();
            return PromptPartPatchRenderer.RenderList(items, page.NextCursor);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                ex.Message,
                new Dictionary<string, object?>());
        }
    }

    [McpServerTool(Name = "get_prompt_part_patch", ReadOnly = true)]
    [Description("Read one PromptPartPatch by id. Returns the full patch plus the current target prompt part text and version so the agent can show a diff or check whether the patch still applies cleanly.")]
    public async Task<McpToolPayload> GetPromptPartPatch(
        [Description("PromptPartPatch id (32 hex chars).")] string prompt_part_patch_id,
        CancellationToken cancellationToken = default)
    {
        var view = await getHandler.HandleAsync(prompt_part_patch_id, cancellationToken);
        return PromptPartPatchRenderer.RenderDetail(PromptPartPatchMcpMapper.ToReadModel(view.Patch), view);
    }

    [McpServerTool(Name = "get_current_prompt_part", ReadOnly = true)]
    [Description("Read the current text and version of a prompt part by (target_scope, target_key). Use this to populate base_version when calling propose_prompt_part_patch. For dream typically scope=\"user\", key in {work, interview}.")]
    public async Task<McpToolPayload> GetCurrentPromptPart(
        [Description("Scope of the prompt part: system | user.")] string target_scope,
        [Description("Key of the prompt part.")] string target_key,
        CancellationToken cancellationToken = default)
    {
        var view = await currentHandler.HandleAsync(target_scope, target_key, cancellationToken);
        return CurrentPromptPartRenderer.Render(view);
    }
}

internal static class PromptPartPatchMcpMapper
{
    public static McpPromptPartPatchReadModel ToReadModel(PromptPartPatch patch) => new(
        patch.Identity.Id,
        patch.Identity.TargetScope,
        patch.Identity.TargetKey,
        patch.State.Status,
        patch.PatchText,
        patch.State.AppliedText,
        patch.EvidenceCardIds.ToList(),
        patch.Rationale,
        patch.State.RejectComment,
        patch.Identity.BaseVersion,
        patch.State.AppliedVersion,
        patch.Identity.CreatedAt,
        patch.State.UpdatedAt,
        patch.State.DecidedAt);
}

public sealed record McpPromptPartPatchReadModel(
    [property: Description("PromptPartPatch id (32 hex chars).")] string Id,
    [property: Description("Target prompt part scope.")] string TargetScope,
    [property: Description("Target prompt part key.")] string TargetKey,
    [property: Description("Lifecycle status: proposed | applied | applied_edited | rejected | superseded.")] string Status,
    [property: Description("Original proposal as submitted by the agent.")] string PatchText,
    [property: Description("What the user actually applied; null until decided.")] string? AppliedText,
    [property: Description("Opaque agent-side evidence ids cited at propose time; stored verbatim.")] IReadOnlyList<string> EvidenceCardIds,
    [property: Description("Short rationale (≤500 chars).")] string Rationale,
    [property: Description("Operator-supplied reject reason; present iff status == rejected.")] string? RejectComment,
    [property: Description("PromptPart.current_version the patch is based on.")] int BaseVersion,
    [property: Description("Post-apply PromptPart.current_version; null until applied.")] int? AppliedVersion,
    [property: Description("Patch creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Last status / payload change (UTC).")] DateTimeOffset UpdatedAt,
    [property: Description("Apply / reject timestamp (UTC); null while still proposed.")] DateTimeOffset? DecidedAt);
