namespace Throne.Domain.Instructions;

public static class InstructionKindNames
{
    public const string Common = "common";
    public const string Interview = "interview";
    public const string Work = "work";
    public const string Dream = "dream";

    /// <summary>
    /// System-only kind carrying the "how to build the repository schema map" instructions
    /// (ADR-0031). It participates in the <c>schema_map</c> bundle but has no user-scope
    /// counterpart — there is no per-user <c>schema_map</c> instruction.
    /// </summary>
    public const string SchemaMap = "schema_map";

    public static readonly IReadOnlyList<string> All =
    [
        Common,
        Interview,
        Work,
        Dream,
        SchemaMap,
    ];

    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);
}
