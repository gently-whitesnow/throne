namespace Throne.Domain.PromptParts;

/// <summary>
/// Length and range budgets for <see cref="PromptPartPatch"/>. Kept as a separate helper
/// so the budget constants stay close to their enforcement without bloating the
/// aggregate's surface.
/// </summary>
internal static class PromptPartPatchBudgets
{
    public static void EnsureAll(
        string patchText,
        string rationale,
        int baseVersion)
    {
        EnsurePatchTextWithinBudget(patchText);
        EnsureRationaleWithinBudget(rationale);
        EnsureBaseVersionPositive(baseVersion);
    }

    private static void EnsurePatchTextWithinBudget(string patchText)
    {
        if (patchText.Length > PromptPartPatch.MaxPatchTextLength)
        {
            throw new ArgumentException(
                $"patch_text must be at most {PromptPartPatch.MaxPatchTextLength} characters.",
                nameof(patchText));
        }
    }

    private static void EnsureRationaleWithinBudget(string rationale)
    {
        if (rationale.Length > PromptPartPatch.MaxRationaleLength)
        {
            throw new ArgumentException(
                $"rationale must be at most {PromptPartPatch.MaxRationaleLength} characters.",
                nameof(rationale));
        }
    }

    private static void EnsureBaseVersionPositive(int baseVersion)
    {
        // 0 — допустимое значение: первичный патч на части, которой ещё нет
        // (apply создаст PromptPart лениво с patch.PatchText как v1).
        if (baseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseVersion),
                "base_version must be ≥ 0.");
        }
    }
}
