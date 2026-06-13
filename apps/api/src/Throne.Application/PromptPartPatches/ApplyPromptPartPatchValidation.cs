using Throne.Domain.PromptParts;

namespace Throne.Application.PromptPartPatches;

/// <summary>
/// Pre-flight checks for <see cref="ApplyPromptPartPatchHandler"/>. Pure — only operates on
/// the values passed in.
/// </summary>
internal static class ApplyPromptPartPatchValidation
{
    public static void EnsureBaseVersionMatches(PromptPartPatch patch, PromptPart? part)
    {
        if (part is null && patch.Identity.BaseVersion != 0)
        {
            throw PromptPartPatchExceptions.PartNotFound(patch.Identity.TargetScope, patch.Identity.TargetKey);
        }
        if (part is not null && part.CurrentVersion != patch.Identity.BaseVersion)
        {
            throw PromptPartPatchExceptions.NeedsRebase(patch, part.CurrentVersion);
        }
    }
}
