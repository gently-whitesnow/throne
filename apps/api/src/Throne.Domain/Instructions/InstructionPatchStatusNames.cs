namespace Throne.Domain.Instructions;

/// <summary>
/// Lifecycle of <see cref="InstructionPatch"/>. Transitions are user-driven
/// (apply / apply-with-edit / reject); the agent only ever creates patches in
/// <see cref="Proposed"/>. <see cref="Superseded"/> is reserved for future
/// dedup passes (e.g. when a newer patch on the same target invalidates an
/// earlier proposal); this iteration writes that status only via Restore.
/// </summary>
public static class InstructionPatchStatusNames
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
