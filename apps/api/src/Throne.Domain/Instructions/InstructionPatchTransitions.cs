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
        if (patch.State.Status != InstructionPatchStatusNames.Proposed)
        {
            return ApplyResult.AlreadyDecided;
        }
        if (appliedInstructionVersion < patch.Identity.BaseInstructionVersion + 1)
        {
            return ApplyResult.InvalidAppliedVersion;
        }

        var (status, appliedText) = ResolveApplied(editedText, patch.PatchText);
        patch.State = new InstructionPatchState(
            Status: status,
            AppliedText: appliedText,
            RejectComment: patch.State.RejectComment,
            AppliedInstructionVersion: appliedInstructionVersion,
            UpdatedAt: now,
            DecidedAt: now);
        return ApplyResult.Ok;
    }

    public static RejectResult Reject(InstructionPatch patch, string comment, DateTimeOffset now)
    {
        if (patch.State.Status != InstructionPatchStatusNames.Proposed)
        {
            return RejectResult.AlreadyDecided;
        }
        var trimmed = (comment ?? string.Empty).Trim();
        if (trimmed.Length < InstructionPatch.MinRejectCommentLength)
        {
            return RejectResult.CommentTooShort;
        }
        patch.State = new InstructionPatchState(
            Status: InstructionPatchStatusNames.Rejected,
            AppliedText: patch.State.AppliedText,
            RejectComment: trimmed,
            AppliedInstructionVersion: patch.State.AppliedInstructionVersion,
            UpdatedAt: now,
            DecidedAt: now);
        return RejectResult.Ok;
    }

    private static (string Status, string AppliedText) ResolveApplied(string? editedText, string patchText)
    {
        if (string.IsNullOrEmpty(editedText) || string.Equals(editedText, patchText, StringComparison.Ordinal))
        {
            return (InstructionPatchStatusNames.Applied, patchText);
        }
        return (InstructionPatchStatusNames.AppliedEdited, editedText);
    }
}
