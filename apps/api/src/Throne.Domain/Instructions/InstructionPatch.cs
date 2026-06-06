namespace Throne.Domain.Instructions;

/// <summary>
/// Patch proposal against a single user-instruction kind, produced by a
/// frontier agent and decided by the operator (apply / apply-with-edit / reject).
/// Replaces the DreamRun + DreamProposal pair from ADR-0011 with a flat,
/// first-class entity per [ADR-0021].
///
/// Domain invariants:
///   • <see cref="PatchText"/> is the agent's original proposal — never mutated;
///   • <see cref="InstructionPatchState.AppliedText"/> is non-null iff the user
///     applied the patch (verbatim → equal to <see cref="PatchText"/>; edited →
///     divergent);
///   • <see cref="InstructionPatchState.RejectComment"/> ≥ 10 chars after
///     trimming and is required for
///     <see cref="InstructionPatchStatusNames.Rejected"/>;
///   • <see cref="InstructionPatchState.AppliedInstructionVersion"/> is the
///     post-replace <c>Instruction.current_version</c> (= base + 1) and is set
///     only by the apply path;
///   • <see cref="Rationale"/> ≤ 500 characters.
///
/// State transitions are exposed as instance methods (<see cref="Apply"/>,
/// <see cref="Reject"/>) which return the corresponding result enum without
/// throwing — callers translate the outcome into protocol-level errors.
/// </summary>
public sealed class InstructionPatch
{
    public const int MinRejectCommentLength = 10;
    public const int MaxRationaleLength = 500;
    public const int MaxPatchTextLength = 32_000;
    public const int MaxEvidenceCardIds = 50;

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

    private InstructionPatch(
        InstructionPatchIdentity identity,
        InstructionPatchState state,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale)
    {
        Identity = identity;
        State = state;
        PatchText = patchText;
        EvidenceCardIds = evidenceCardIds;
        Rationale = rationale;
    }

    public InstructionPatchIdentity Identity { get; }
    public string PatchText { get; }
    public IReadOnlyList<string> EvidenceCardIds { get; }
    public string Rationale { get; }
    public InstructionPatchState State { get; private set; }

    public static InstructionPatch Create(
        string id,
        string targetKind,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale,
        int baseInstructionVersion,
        DateTimeOffset now)
    {
        EnsureRequiredStringsForCreate(id, targetKind, rationale);
        ArgumentNullException.ThrowIfNull(patchText);
        ArgumentNullException.ThrowIfNull(evidenceCardIds);
        EnsureKnownKind(targetKind);
        InstructionPatchBudgets.EnsureAll(patchText, evidenceCardIds, rationale, baseInstructionVersion);

        var identity = new InstructionPatchIdentity(id, targetKind, baseInstructionVersion, now);
        return new InstructionPatch(
            identity,
            InstructionPatchState.Initial(now),
            patchText,
            [.. evidenceCardIds],
            rationale);
    }

    public static InstructionPatch Restore(
        InstructionPatchIdentity identity,
        InstructionPatchState state,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(identity.Id);
        EnsureKnownKind(identity.TargetKind);
        EnsureKnownStatus(state.Status);
        return new InstructionPatch(identity, state, patchText, [.. evidenceCardIds], rationale);
    }

    /// <summary>
    /// User-driven apply transition. Verbatim apply (<paramref name="editedText"/>
    /// null or equal to <see cref="PatchText"/>) flips status to
    /// <see cref="InstructionPatchStatusNames.Applied"/>; a divergent
    /// <paramref name="editedText"/> flips to
    /// <see cref="InstructionPatchStatusNames.AppliedEdited"/>.
    /// </summary>
    public ApplyResult Apply(string? editedText, int appliedInstructionVersion, DateTimeOffset now)
    {
        if (State.Status != InstructionPatchStatusNames.Proposed)
        {
            return ApplyResult.AlreadyDecided;
        }
        if (appliedInstructionVersion < Identity.BaseInstructionVersion + 1)
        {
            return ApplyResult.InvalidAppliedVersion;
        }

        var (status, appliedText) = ResolveApplied(editedText, PatchText);
        State = new InstructionPatchState(
            Status: status,
            AppliedText: appliedText,
            RejectComment: State.RejectComment,
            AppliedInstructionVersion: appliedInstructionVersion,
            UpdatedAt: now,
            DecidedAt: now);
        return ApplyResult.Ok;
    }

    /// <summary>
    /// User-driven reject transition. Requires a non-empty
    /// <paramref name="comment"/> with at least <see cref="MinRejectCommentLength"/>
    /// characters after trimming.
    /// </summary>
    public RejectResult Reject(string comment, DateTimeOffset now)
    {
        if (State.Status != InstructionPatchStatusNames.Proposed)
        {
            return RejectResult.AlreadyDecided;
        }
        var trimmed = (comment ?? string.Empty).Trim();
        if (trimmed.Length < MinRejectCommentLength)
        {
            return RejectResult.CommentTooShort;
        }
        State = new InstructionPatchState(
            Status: InstructionPatchStatusNames.Rejected,
            AppliedText: State.AppliedText,
            RejectComment: trimmed,
            AppliedInstructionVersion: State.AppliedInstructionVersion,
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

    private static void EnsureRequiredStringsForCreate(
        string id,
        string targetKind,
        string rationale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);
    }

    private static void EnsureKnownKind(string kind)
    {
        if (!InstructionKindNames.IsKnown(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                $"Unknown instruction kind: {kind}.");
        }
    }

    private static void EnsureKnownStatus(string status)
    {
        if (!InstructionPatchStatusNames.IsKnown(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                $"Unknown InstructionPatch status: {status}.");
        }
    }
}
