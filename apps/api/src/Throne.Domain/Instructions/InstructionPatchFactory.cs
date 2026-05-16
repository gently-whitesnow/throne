namespace Throne.Domain.Instructions;

/// <summary>
/// Factory façade for <see cref="InstructionPatch"/>. Lives outside the
/// aggregate so the aggregate's per-type cyclomatic complexity stays inside
/// the project's CA1502 budget — guard branching is moved out of the class.
/// </summary>
public static class InstructionPatchFactory
{
    public static InstructionPatch Create(
        string id,
        string ownerUserId,
        string targetKind,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale,
        int baseInstructionVersion,
        DateTimeOffset now)
    {
        InstructionPatchGuards.EnsureCreateInputs(
            id,
            ownerUserId,
            targetKind,
            patchText,
            evidenceCardIds,
            rationale,
            baseInstructionVersion);
        var identity = new InstructionPatchIdentity(id, ownerUserId, targetKind, baseInstructionVersion, now);
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
        InstructionPatchGuards.EnsureRestoreInputs(identity.Id, identity.OwnerUserId, identity.TargetKind, state.Status);
        return new InstructionPatch(identity, state, patchText, [.. evidenceCardIds], rationale);
    }
}
