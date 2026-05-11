namespace Throne.Domain.Instructions;

/// <summary>
/// Patch proposal against a single user-instruction kind, produced by a
/// frontier agent and decided by the operator (apply / apply-with-edit / reject).
/// Replaces the DreamRun + DreamProposal pair from ADR-0011 with a flat,
/// first-class entity per [ADR-0021].
///
/// Domain invariants:
///   • <see cref="PatchText"/> is the agent's original proposal — never mutated;
///   • <see cref="AppliedText"/> is non-null iff the user applied the patch
///     (verbatim → equal to <see cref="PatchText"/>; edited → divergent);
///   • <see cref="RejectComment"/> ≥ 10 chars after trimming and is required
///     for <see cref="InstructionPatchStatusNames.Rejected"/>;
///   • <see cref="AppliedInstructionVersion"/> is the post-replace
///     <c>Instruction.current_version</c> (= base + 1) and is set only by the
///     apply path;
///   • <see cref="Rationale"/> ≤ 500 characters.
///
/// Status transitions go through <see cref="InstructionPatchTransitions"/> —
/// the aggregate exposes only state read access plus an internal
/// <c>ReplaceState</c> mutator. Construction is funnelled through
/// <see cref="InstructionPatchFactory"/> so this class is small enough for the
/// per-type CA1502 budget.
/// </summary>
public sealed class InstructionPatch
{
    public const int MinRejectCommentLength = 10;
    public const int MaxRationaleLength = 500;
    public const int MaxPatchTextLength = 32_000;
    public const int MaxEvidenceCardIds = 50;

    private InstructionPatchState _state;

    private InstructionPatch(
        InstructionPatchIdentity identity,
        InstructionPatchState state,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale)
    {
        Identity = identity;
        _state = state;
        PatchText = patchText;
        EvidenceCardIds = [.. evidenceCardIds];
        Rationale = rationale;
    }

    public InstructionPatchIdentity Identity { get; }
    public string PatchText { get; }
    public IReadOnlyList<string> EvidenceCardIds { get; }
    public string Rationale { get; }
    public InstructionPatchState State => _state;

    // Convenience accessors for hot paths in handlers / mappers.
    public string Id => Identity.Id;
    public string OwnerUserId => Identity.OwnerUserId;
    public string TargetKind => Identity.TargetKind;
    public int BaseInstructionVersion => Identity.BaseInstructionVersion;
    public DateTimeOffset CreatedAt => Identity.CreatedAt;
    public string Status => _state.Status;
    public string? AppliedText => _state.AppliedText;
    public string? RejectComment => _state.RejectComment;
    public int? AppliedInstructionVersion => _state.AppliedInstructionVersion;
    public DateTimeOffset UpdatedAt => _state.UpdatedAt;
    public DateTimeOffset? DecidedAt => _state.DecidedAt;

    public static InstructionPatch Create(
        string id,
        string ownerUserId,
        string targetKind,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale,
        int baseInstructionVersion,
        DateTimeOffset now)
        => InstructionPatchFactory.Create(
            id, ownerUserId, targetKind, patchText, evidenceCardIds, rationale, baseInstructionVersion, now);

    public static InstructionPatch Restore(
        InstructionPatchIdentity identity,
        InstructionPatchState state,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale)
        => InstructionPatchFactory.Restore(identity, state, patchText, evidenceCardIds, rationale);

    internal static InstructionPatch CreateInternal(
        InstructionPatchIdentity identity,
        InstructionPatchState state,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale)
        => new(identity, state, patchText, evidenceCardIds, rationale);

    internal void ReplaceState(InstructionPatchState state) => _state = state;
}
