using Throne.Application.Errors;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Shared <see cref="ApiException"/> factory for InstructionPatch handlers.
/// Lifted out of the handlers to keep their per-type CA1502 budget within
/// the project threshold (≤10).
/// </summary>
internal static class InstructionPatchExceptions
{
    public static ApiException NotFound(string patchId) => new(
        ErrorCodes.InstructionPatchNotFound,
        $"InstructionPatch '{patchId}' not found.",
        new Dictionary<string, object?> { ["patch_id"] = patchId });

    public static ApiException AlreadyDecided(InstructionPatch patch) => new(
        ErrorCodes.InstructionPatchAlreadyDecided,
        $"InstructionPatch '{patch.Identity.Id}' is in status '{patch.State.Status}'.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Identity.Id,
            ["current_status"] = patch.State.Status,
        });

    public static ApiException NeedsRebase(InstructionPatch patch, int currentVersion) => new(
        ErrorCodes.InstructionPatchNeedsRebase,
        "Instruction.current_version moved past patch.base_instruction_version.",
        new Dictionary<string, object?>
        {
            ["patch_id"] = patch.Identity.Id,
            ["target_kind"] = patch.Identity.TargetKind,
            ["base_instruction_version"] = patch.Identity.BaseInstructionVersion,
            ["current_instruction_version"] = currentVersion,
        });

    public static ApiException InstructionNotFound(string targetKind) => new(
        ErrorCodes.InstructionNotFound,
        $"User instruction with kind '{targetKind}' not found.",
        new Dictionary<string, object?> { ["target_kind"] = targetKind });

    public static ApiException CommentTooShort() => new(
        ErrorCodes.ValidationFailed,
        $"reject_comment must be at least {InstructionPatch.MinRejectCommentLength} characters after trimming.",
        new Dictionary<string, object?>
        {
            ["field"] = "reject_comment",
            ["min_length"] = InstructionPatch.MinRejectCommentLength,
        });

    public static ApiException ValidationFailed(string field, string message) => new(
        ErrorCodes.ValidationFailed,
        message,
        new Dictionary<string, object?> { ["field"] = field });

    public static ApiException UnknownTargetKind(string targetKind) =>
        ValidationFailed("target_kind", $"Unknown target_kind: {targetKind}.");
}
