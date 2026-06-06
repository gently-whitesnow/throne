using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.InstructionPatches;

/// <summary>
/// Translates persistence-layer outcome unions into either a successful aggregate
/// or an <see cref="ApiException"/>. Shared across the apply/reject handlers.
/// </summary>
internal static class InstructionPatchOutcomeMapper
{
    public static Instruction UnwrapReplace(
        ReplaceInstructionTextOutcome outcome,
        InstructionPatch patch,
        string instructionId) =>
        outcome switch
        {
            ReplaceInstructionTextOutcome.Replaced replaced => replaced.Instruction,
            ReplaceInstructionTextOutcome.VersionConflict vc =>
                throw InstructionPatchExceptions.NeedsRebase(patch, vc.CurrentVersion),
            ReplaceInstructionTextOutcome.NotFound =>
                throw InstructionPatchExceptions.InstructionNotFound(patch.Identity.TargetKind),
            ReplaceInstructionTextOutcome.MatchNotFound matchNotFound => throw new ApiException(
                ErrorCodes.InstructionTextMatchNotFound,
                "old_text was not found in Instruction.text.",
                new Dictionary<string, object?>
                {
                    ["instruction_id"] = instructionId,
                    ["query_preview"] = matchNotFound.QueryPreview,
                }),
            ReplaceInstructionTextOutcome.MatchAmbiguous ambiguous => throw new ApiException(
                ErrorCodes.InstructionTextMatchAmbiguous,
                "old_text matches more than one location.",
                new Dictionary<string, object?>
                {
                    ["instruction_id"] = instructionId,
                    ["matches_count"] = ambiguous.MatchesCount,
                }),
            _ => throw new InvalidOperationException(
                $"Unhandled instruction replace outcome: {outcome.GetType().Name}"),
        };

    public static InstructionPatch UnwrapApply(
        ApplyInstructionPatchPersistenceOutcome outcome,
        string patchId) =>
        outcome switch
        {
            ApplyInstructionPatchPersistenceOutcome.Applied applied => applied.Patch,
            ApplyInstructionPatchPersistenceOutcome.AlreadyDecided ad =>
                throw InstructionPatchExceptions.AlreadyDecided(ad.Patch),
            ApplyInstructionPatchPersistenceOutcome.NotFound =>
                throw InstructionPatchExceptions.NotFound(patchId),
            _ => throw new InvalidOperationException(
                $"Unhandled patch apply outcome: {outcome.GetType().Name}"),
        };

    public static InstructionPatch UnwrapReject(
        RejectInstructionPatchPersistenceOutcome outcome,
        string patchId) =>
        outcome switch
        {
            RejectInstructionPatchPersistenceOutcome.Rejected rejected => rejected.Patch,
            RejectInstructionPatchPersistenceOutcome.AlreadyDecided ad =>
                throw InstructionPatchExceptions.AlreadyDecided(ad.Patch),
            RejectInstructionPatchPersistenceOutcome.NotFound =>
                throw InstructionPatchExceptions.NotFound(patchId),
            _ => throw new InvalidOperationException(
                $"Unhandled reject outcome: {outcome.GetType().Name}"),
        };

    public static void ThrowForRejectTransition(
        InstructionPatch.RejectResult transition,
        InstructionPatch patch)
    {
        switch (transition)
        {
            case InstructionPatch.RejectResult.Ok:
                return;
            case InstructionPatch.RejectResult.AlreadyDecided:
                throw InstructionPatchExceptions.AlreadyDecided(patch);
            case InstructionPatch.RejectResult.CommentTooShort:
                throw InstructionPatchExceptions.CommentTooShort();
            default:
                throw new InvalidOperationException($"Unhandled reject result: {transition}");
        }
    }
}
