namespace Throne.Domain.Instructions;

/// <summary>
/// Pure transition logic for <see cref="InstructionPatch"/>. Lives outside the
/// aggregate so its branching does not push the aggregate above the per-type
/// CA1502 budget; the aggregate exposes thin wrappers that delegate here and
/// applies the resulting state via <see cref="InstructionPatch.ReplaceState"/>.
/// </summary>
public static class InstructionPatchTransitions
{
    public enum ApplyResult
    {
        Ok,
        AlreadyDecided,
        InvalidAppliedVersion,
    }

    public enum RejectResult
    {
        Ok,
        AlreadyDecided,
        CommentTooShort,
    }

    public static ApplyResult Apply(
        InstructionPatch patch,
        string? editedText,
        int appliedInstructionVersion,
        DateTimeOffset now)
    {
        if (patch.Status != InstructionPatchStatusNames.Proposed)
        {
            return ApplyResult.AlreadyDecided;
        }
        if (appliedInstructionVersion < patch.BaseInstructionVersion + 1)
        {
            return ApplyResult.InvalidAppliedVersion;
        }

        var verbatim = IsVerbatim(editedText, patch.PatchText);
        var appliedText = verbatim ? patch.PatchText : editedText!;
        var status = verbatim
            ? InstructionPatchStatusNames.Applied
            : InstructionPatchStatusNames.AppliedEdited;
        patch.ReplaceState(new InstructionPatchState(
            Status: status,
            AppliedText: appliedText,
            RejectComment: patch.RejectComment,
            AppliedInstructionVersion: appliedInstructionVersion,
            UpdatedAt: now,
            DecidedAt: now));
        return ApplyResult.Ok;
    }

    public static RejectResult Reject(InstructionPatch patch, string comment, DateTimeOffset now)
    {
        if (patch.Status != InstructionPatchStatusNames.Proposed)
        {
            return RejectResult.AlreadyDecided;
        }
        var trimmed = (comment ?? string.Empty).Trim();
        if (trimmed.Length < InstructionPatch.MinRejectCommentLength)
        {
            return RejectResult.CommentTooShort;
        }
        patch.ReplaceState(new InstructionPatchState(
            Status: InstructionPatchStatusNames.Rejected,
            AppliedText: patch.AppliedText,
            RejectComment: trimmed,
            AppliedInstructionVersion: patch.AppliedInstructionVersion,
            UpdatedAt: now,
            DecidedAt: now));
        return RejectResult.Ok;
    }

    private static bool IsVerbatim(string? editedText, string patchText)
    {
        if (string.IsNullOrEmpty(editedText))
        {
            return true;
        }
        return string.Equals(editedText, patchText, StringComparison.Ordinal);
    }
}
