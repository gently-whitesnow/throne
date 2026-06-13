using Throne.Application.Errors;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// Shared <see cref="ApiException"/> factory for PromptPartPatch handlers.
/// </summary>
internal static class PromptPartPatchExceptions
{
    public static ApiException NotFound(string patchId) => new(
        ErrorCodes.PromptPartPatchNotFound,
        $"PromptPartPatch '{patchId}' not found.",
        new Dictionary<string, object?> { ["patch_id"] = patchId });

    public static ApiException AlreadyDecided(PromptPartPatch patch) => new(
        ErrorCodes.PromptPartPatchAlreadyDecided,
        $"PromptPartPatch '{patch.Identity.Id}' is in status '{patch.State.Status}'.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Identity.Id,
            ["current_status"] = patch.State.Status,
        });

    public static ApiException NeedsRebase(PromptPartPatch patch, int currentVersion) => new(
        ErrorCodes.PromptPartPatchNeedsRebase,
        "PromptPart.current_version moved past patch.base_version.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Identity.Id,
            ["target_scope"] = patch.Identity.TargetScope,
            ["target_key"] = patch.Identity.TargetKey,
            ["base_version"] = patch.Identity.BaseVersion,
            ["current_version"] = currentVersion,
        });

    public static ApiException PartNotFound(string targetScope, string targetKey) => new(
        ErrorCodes.PromptPartNotFound,
        $"Prompt part ({targetScope}, {targetKey}) not found.",
        new Dictionary<string, object?>
        {
            ["target_scope"] = targetScope,
            ["target_key"] = targetKey,
        });

    public static ApiException CommentTooShort() => new(
        ErrorCodes.ValidationFailed,
        $"reject_comment must be at least {PromptPartPatch.MinRejectCommentLength} characters after trimming.",
        new Dictionary<string, object?>
        {
            ["field"] = "reject_comment",
            ["min_length"] = PromptPartPatch.MinRejectCommentLength,
        });

    public static ApiException ValidationFailed(string field, string message) => new(
        ErrorCodes.ValidationFailed,
        message,
        new Dictionary<string, object?> { ["field"] = field });

    public static ApiException UnknownTargetScope(string targetScope) =>
        ValidationFailed("target_scope", $"Unknown target_scope: {targetScope}.");

    public static ApiException UnpatchableTargetScope(string targetScope) => new(
        ErrorCodes.ValidationFailed,
        "PromptPartPatch targets must use target_scope='user'. System prompt parts are manifest-managed.",
        new Dictionary<string, object?>
        {
            ["field"] = "target_scope",
            ["target_scope"] = targetScope,
            ["allowed_scope"] = PromptPartScopeNames.User,
        });
}
