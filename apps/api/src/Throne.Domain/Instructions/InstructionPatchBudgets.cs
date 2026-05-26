namespace Throne.Domain.Instructions;

/// <summary>
/// Length and range budgets for <see cref="InstructionPatch"/>. Kept as a
/// separate helper so the budget constants stay close to their enforcement
/// without bloating the aggregate's surface.
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
        // 0 — допустимое значение: первичный патч на пользователе, у которого
        // ещё нет user-инструкции выбранного kind (apply создаст Instruction
        // лениво с patch.PatchText как v1). Отрицательные значения недопустимы.
        if (baseInstructionVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseInstructionVersion),
                "base_instruction_version must be ≥ 0.");
        }
    }
}
