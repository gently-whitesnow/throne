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
/// Status transitions go through <see cref="InstructionPatchTransitions"/> —
/// the aggregate exposes only state read access plus a single internal
/// <c>State</c> setter. Construction is funnelled through
/// <see cref="InstructionPatchFactory"/> so this class is small enough for the
/// per-type CA1502 budget.
/// </summary>
public sealed class InstructionPatch
{
    public const int MinRejectCommentLength = 10;
    public const int MaxRationaleLength = 500;
    public const int MaxPatchTextLength = 32_000;
    public const int MaxEvidenceCardIds = 50;

    internal InstructionPatch(
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
    public InstructionPatchState State { get; internal set; }

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
}
