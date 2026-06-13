namespace Throne.Domain.PromptParts;

/// <summary>
/// Lifecycle of <see cref="PromptPartPatch"/>. Transitions are user-driven
/// (apply / apply-with-edit / reject); the agent only ever creates patches in
/// <see cref="Proposed"/>. <see cref="Superseded"/> is reserved for future
/// dedup passes; this iteration writes that status only via Restore.
/// </summary>
public static class PromptPartPatchStatusNames
{
    public const string Proposed = "proposed";
    public const string Applied = "applied";
    public const string AppliedEdited = "applied_edited";
    public const string Rejected = "rejected";
    public const string Superseded = "superseded";

    public static bool IsKnown(string value) => value switch
    {
        Proposed or Applied or AppliedEdited or Rejected or Superseded => true,
        _ => false,
    };

    public static bool IsTerminal(string value) => value switch
    {
        Applied or AppliedEdited or Rejected or Superseded => true,
        _ => false,
    };

    public static bool IsApplied(string value) => value switch
    {
        Applied or AppliedEdited => true,
        _ => false,
    };
}
