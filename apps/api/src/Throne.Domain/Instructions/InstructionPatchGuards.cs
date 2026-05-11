namespace Throne.Domain.Instructions;

/// <summary>
/// Public guard entry-points used by <see cref="InstructionPatchFactory"/>.
/// Delegate to <see cref="InstructionPatchBudgets"/> for length / range checks
/// so each helper class stays inside the per-type CA1502 budget.
/// </summary>
internal static class InstructionPatchGuards
{
    public static void EnsureCreateInputs(
        string id,
        string ownerUserId,
        string targetKind,
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale,
        int baseInstructionVersion)
    {
        EnsureRequiredStringsForCreate(id, ownerUserId, targetKind, rationale);
        ArgumentNullException.ThrowIfNull(patchText);
        ArgumentNullException.ThrowIfNull(evidenceCardIds);
        EnsureKnownKind(targetKind);
        InstructionPatchBudgets.EnsureAll(patchText, evidenceCardIds, rationale, baseInstructionVersion);
    }

    public static void EnsureRestoreInputs(
        string id,
        string ownerUserId,
        string targetKind,
        string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
        EnsureKnownKind(targetKind);
        EnsureKnownStatus(status);
    }

    private static void EnsureRequiredStringsForCreate(
        string id,
        string ownerUserId,
        string targetKind,
        string rationale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerUserId);
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
