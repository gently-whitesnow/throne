using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// Translates persistence-layer outcome unions into either a successful aggregate
/// or an <see cref="ApiException"/>.
/// </summary>
internal static class PromptPartPatchOutcomeMapper
{
    public static PromptPart UnwrapReplace(
        ReplacePromptPartTextOutcome outcome,
        PromptPartPatch patch,
        string promptPartId) =>
        outcome switch
        {
            ReplacePromptPartTextOutcome.Replaced replaced => replaced.Part,
            ReplacePromptPartTextOutcome.VersionConflict vc =>
                throw PromptPartPatchExceptions.NeedsRebase(patch, vc.CurrentVersion),
            ReplacePromptPartTextOutcome.NotFound =>
                throw PromptPartPatchExceptions.PartNotFound(patch.Identity.TargetScope, patch.Identity.TargetKey),
            ReplacePromptPartTextOutcome.MatchNotFound matchNotFound => throw new ApiException(
                ErrorCodes.PromptPartTextMatchFailed,
                "old_text was not found in PromptPart.text.",
                new Dictionary<string, object?>
                {
                    ["prompt_part_id"] = promptPartId,
                    ["reason"] = "match_not_found",
                    ["query_preview"] = matchNotFound.QueryPreview,
                }),
            ReplacePromptPartTextOutcome.MatchAmbiguous ambiguous => throw new ApiException(
                ErrorCodes.PromptPartTextMatchFailed,
                "old_text matches more than one location.",
                new Dictionary<string, object?>
                {
                    ["prompt_part_id"] = promptPartId,
                    ["reason"] = "match_ambiguous",
                    ["matches_count"] = ambiguous.MatchesCount,
                }),
            _ => throw new InvalidOperationException(
                $"Unhandled prompt part replace outcome: {outcome.GetType().Name}"),
        };

    public static PromptPartPatch UnwrapApply(
        ApplyPromptPartPatchPersistenceOutcome outcome,
        string patchId) =>
        outcome switch
        {
            ApplyPromptPartPatchPersistenceOutcome.Applied applied => applied.Patch,
            ApplyPromptPartPatchPersistenceOutcome.AlreadyDecided ad =>
                throw PromptPartPatchExceptions.AlreadyDecided(ad.Patch),
            ApplyPromptPartPatchPersistenceOutcome.NotFound =>
                throw PromptPartPatchExceptions.NotFound(patchId),
            _ => throw new InvalidOperationException(
                $"Unhandled patch apply outcome: {outcome.GetType().Name}"),
        };

    public static PromptPartPatch UnwrapReject(
        RejectPromptPartPatchPersistenceOutcome outcome,
        string patchId) =>
        outcome switch
        {
            RejectPromptPartPatchPersistenceOutcome.Rejected rejected => rejected.Patch,
            RejectPromptPartPatchPersistenceOutcome.AlreadyDecided ad =>
                throw PromptPartPatchExceptions.AlreadyDecided(ad.Patch),
            RejectPromptPartPatchPersistenceOutcome.NotFound =>
                throw PromptPartPatchExceptions.NotFound(patchId),
            _ => throw new InvalidOperationException(
                $"Unhandled reject outcome: {outcome.GetType().Name}"),
        };

    public static void ThrowForRejectTransition(
        PromptPartPatch.RejectResult transition,
        PromptPartPatch patch)
    {
        switch (transition)
        {
            case PromptPartPatch.RejectResult.Ok:
                return;
            case PromptPartPatch.RejectResult.AlreadyDecided:
                throw PromptPartPatchExceptions.AlreadyDecided(patch);
            case PromptPartPatch.RejectResult.CommentTooShort:
                throw PromptPartPatchExceptions.CommentTooShort();
            default:
                throw new InvalidOperationException($"Unhandled reject result: {transition}");
        }
    }
}
