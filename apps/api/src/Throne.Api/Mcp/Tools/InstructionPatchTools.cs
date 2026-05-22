using System.ComponentModel;
using ModelContextProtocol.Server;
using Throne.Application.Errors;
using Throne.Application.InstructionPatches;
using Throne.Domain.Instructions;

namespace Throne.Api.Mcp.Tools;

/// <summary>
/// MCP surface for InstructionPatch (ADR-0021 supersedes ADR-0011). Four tools
/// land in this iteration:
///   * <c>propose_instruction_patch</c> — frontier creates a fresh proposed patch;
///   * <c>list_instruction_patches</c> — paginated owner-scoped list (newest first);
///   * <c>get_instruction_patch</c> — single patch with current target text;
///   * <c>get_current_instruction</c> — current text+version of a user instruction
///     so the agent can ground <c>base_instruction_version</c>.
///
/// Apply / edit / reject live on the HTTP surface — they are user actions, not
/// agent actions. Trying to expose them via MCP would invite agents to apply
/// their own proposals, which ADR-0021 explicitly forbids.
/// </summary>
[McpServerToolType]
public sealed class InstructionPatchTools(
    ProposeInstructionPatchHandler proposeHandler,
    ListInstructionPatchesHandler listHandler,
    GetInstructionPatchHandler getHandler,
    GetCurrentInstructionHandler currentHandler)
{
    [McpServerTool(Name = "propose_instruction_patch", UseStructuredContent = true)]
    [Description("Propose a new InstructionPatch in status 'proposed' for one of the caller's user instructions. base_instruction_version must match the live Instruction.current_version (use get_current_instruction to read it). evidence_card_ids are opaque agent-side references (the server treats them as a free-form audit trail). Apply / reject is a user action via UI/HTTP — agents cannot decide their own proposals. Pass a unique idempotency_key per logical proposal so a transport-level retry returns the original patch instead of creating a duplicate.")]
    public async Task<McpInstructionPatchReadModel> ProposeInstructionPatch(
        [Description("Target user instruction kind: common | interview | work | dream | transfer.")] string target_kind,
        [Description("Whole new text of the target instruction (the apply path replaces Instruction.text verbatim with this).")] string patch_text,
        [Description("Opaque agent-side evidence ids; stored verbatim on the patch for audit but not validated against any server collection.")] IReadOnlyList<string> evidence_card_ids,
        [Description("Short rationale; ≤500 characters.")] string rationale,
        [Description("Instruction.current_version the agent is editing on top of. Mismatch → 409 instruction_patch.needs_rebase on apply (and on propose if the version already drifted).")] int base_instruction_version,
        [Description("Optional client-generated dedup key (≤64 chars). When present, the server returns the previously created patch on a retry with the same key + caller instead of inserting a new one. Recommended for MCP transports without at-most-once delivery (e.g. SSE).")] string? idempotency_key = null,
        CancellationToken cancellationToken = default)
    {
        var patch = await proposeHandler.HandleAsync(
            new ProposeInstructionPatchCommand(
                target_kind,
                patch_text,
                evidence_card_ids ?? Array.Empty<string>(),
                rationale,
                base_instruction_version,
                idempotency_key),
            cancellationToken);
        return InstructionPatchMcpMapper.ToReadModel(patch);
    }

    [McpServerTool(Name = "list_instruction_patches", ReadOnly = true, UseStructuredContent = true)]
    [Description("List InstructionPatches owned by the caller, ordered by created_at descending. Filter by target_kind and/or status. Pagination is opaque-cursor based; pass next_cursor from the previous page to continue. Useful for de-duplication: read past 'rejected' patches with reject_comment to avoid re-proposing the same rule. Empty `items` is a valid success state (no patches in this filter / new user) — do NOT treat it as an error.")]
    public async Task<McpInstructionPatchListResult> ListInstructionPatches(
        [Description("Optional target_kind filter: common | interview | work | dream | transfer.")] string? target_kind = null,
        [Description("Optional status filter: proposed | applied | applied_edited | rejected | superseded.")] string? status = null,
        [Description("Page size, default 50, capped at 200.")] int? limit = null,
        [Description("Opaque cursor returned as next_cursor by the previous page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await listHandler.HandleAsync(
                new ListInstructionPatchesQuery(target_kind, status, limit, cursor),
                cancellationToken);
            return new McpInstructionPatchListResult(
                page.Items.Select(InstructionPatchMcpMapper.ToReadModel).ToList(),
                page.NextCursor);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                ex.Message,
                new Dictionary<string, object?>());
        }
    }

    [McpServerTool(Name = "get_instruction_patch", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read one InstructionPatch by id. Returns the full patch (patch_text, applied_text, reject_comment, evidence_card_ids) plus the current target instruction text and version so the agent can show a diff or check whether the patch still applies cleanly.")]
    public async Task<McpInstructionPatchDetail> GetInstructionPatch(
        [Description("InstructionPatch id (32 hex chars).")] string instruction_patch_id,
        CancellationToken cancellationToken = default)
    {
        var view = await getHandler.HandleAsync(instruction_patch_id, cancellationToken);
        return new McpInstructionPatchDetail(
            InstructionPatchMcpMapper.ToReadModel(view.Patch),
            view.CurrentInstructionText,
            view.CurrentInstructionVersion,
            view.BaseVersionMatchesCurrent,
            view.BaseInstructionText);
    }

    [McpServerTool(Name = "get_current_instruction", ReadOnly = true, UseStructuredContent = true)]
    [Description("Read the current text and version of the caller's user instruction for a given kind. Use this to populate base_instruction_version when calling propose_instruction_patch.")]
    public async Task<McpCurrentInstructionReadModel> GetCurrentInstruction(
        [Description("Instruction kind: common | interview | work | dream | transfer.")] string target_kind,
        CancellationToken cancellationToken = default)
    {
        var view = await currentHandler.HandleAsync(target_kind, cancellationToken);
        return new McpCurrentInstructionReadModel(
            view.InstructionId,
            view.Kind,
            view.Text,
            view.CurrentVersion,
            view.UpdatedAt);
    }
}

internal static class InstructionPatchMcpMapper
{
    public static McpInstructionPatchReadModel ToReadModel(InstructionPatch patch) => new(
        patch.Identity.Id,
        patch.Identity.TargetKind,
        patch.State.Status,
        patch.PatchText,
        patch.State.AppliedText,
        patch.EvidenceCardIds.ToList(),
        patch.Rationale,
        patch.State.RejectComment,
        patch.Identity.BaseInstructionVersion,
        patch.State.AppliedInstructionVersion,
        patch.Identity.CreatedAt,
        patch.State.UpdatedAt,
        patch.State.DecidedAt);
}

public sealed record McpInstructionPatchListResult(
    [property: Description("Page of patches, newest first.")] IReadOnlyList<McpInstructionPatchReadModel> Items,
    [property: Description("Opaque continuation token; null when the page exhausted the result set.")] string? NextCursor);

public sealed record McpInstructionPatchReadModel(
    [property: Description("InstructionPatch id (32 hex chars).")] string Id,
    [property: Description("Target user instruction kind.")] string TargetKind,
    [property: Description("Lifecycle status: proposed | applied | applied_edited | rejected | superseded.")] string Status,
    [property: Description("Original proposal as submitted by the agent.")] string PatchText,
    [property: Description("What the user actually applied; null until decided.")] string? AppliedText,
    [property: Description("Opaque agent-side evidence ids cited at propose time; stored verbatim.")] IReadOnlyList<string> EvidenceCardIds,
    [property: Description("Short rationale (≤500 chars).")] string Rationale,
    [property: Description("Operator-supplied reject reason; present iff status == rejected.")] string? RejectComment,
    [property: Description("Instruction.current_version the patch is based on.")] int BaseInstructionVersion,
    [property: Description("Post-apply Instruction.current_version; null until applied.")] int? AppliedInstructionVersion,
    [property: Description("Patch creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Last status / payload change (UTC).")] DateTimeOffset UpdatedAt,
    [property: Description("Apply / reject timestamp (UTC); null while still proposed.")] DateTimeOffset? DecidedAt);

public sealed record McpInstructionPatchDetail(
    [property: Description("Patch payload.")] McpInstructionPatchReadModel Patch,
    [property: Description("Current text of the target user instruction; empty when missing.")] string CurrentInstructionText,
    [property: Description("Current Instruction.current_version; 0 when missing.")] int CurrentInstructionVersion,
    [property: Description("True when patch.base_instruction_version equals current_instruction_version.")] bool BaseVersionMatchesCurrent,
    [property: Description("Instruction text reconstructed at patch.base_instruction_version (what the agent saw when proposing); empty when base_instruction_version=0 or history unavailable.")] string BaseInstructionText);

public sealed record McpCurrentInstructionReadModel(
    [property: Description("Instruction id (24 hex chars, ObjectId-shaped).")] string InstructionId,
    [property: Description("Instruction kind.")] string Kind,
    [property: Description("Current full instruction text.")] string Text,
    [property: Description("Instruction.current_version.")] int CurrentVersion,
    [property: Description("Last update timestamp (UTC).")] DateTimeOffset UpdatedAt);
