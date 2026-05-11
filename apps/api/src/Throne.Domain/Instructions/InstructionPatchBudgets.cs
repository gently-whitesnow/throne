namespace Throne.Domain.Instructions;

/// <summary>
/// Length and range budgets for <see cref="InstructionPatch"/>. Split from
/// <see cref="InstructionPatchGuards"/> so each helper class stays under the
/// per-type CA1502 budget.
/// </summary>
internal static class InstructionPatchBudgets
{
    public static void EnsureAll(
        string patchText,
        IReadOnlyList<string> evidenceCardIds,
        string rationale,
        int baseInstructionVersion)
    {
        EnsurePatchTextWithinBudget(patchText);
        EnsureEvidenceWithinBudget(evidenceCardIds);
        EnsureRationaleWithinBudget(rationale);
        EnsureBaseVersionPositive(baseInstructionVersion);
    }

    private static void EnsurePatchTextWithinBudget(string patchText)
    {
        if (patchText.Length > InstructionPatch.MaxPatchTextLength)
        {
            throw new ArgumentException(
                $"patch_text must be at most {InstructionPatch.MaxPatchTextLength} characters.",
                nameof(patchText));
        }
    }

    private static void EnsureEvidenceWithinBudget(IReadOnlyList<string> evidenceCardIds)
    {
        if (evidenceCardIds.Count > InstructionPatch.MaxEvidenceCardIds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evidenceCardIds),
                $"evidence_card_ids must contain at most {InstructionPatch.MaxEvidenceCardIds} entries.");
        }
        for (var i = 0; i < evidenceCardIds.Count; i++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(evidenceCardIds[i], nameof(evidenceCardIds));
        }
    }

    private static void EnsureRationaleWithinBudget(string rationale)
    {
        if (rationale.Length > InstructionPatch.MaxRationaleLength)
        {
            throw new ArgumentException(
                $"rationale must be at most {InstructionPatch.MaxRationaleLength} characters.",
                nameof(rationale));
        }
    }

    private static void EnsureBaseVersionPositive(int baseInstructionVersion)
    {
        if (baseInstructionVersion < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseInstructionVersion),
                "base_instruction_version must be ≥ 1.");
        }
    }
}
