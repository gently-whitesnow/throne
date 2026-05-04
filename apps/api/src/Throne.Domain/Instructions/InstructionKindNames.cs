namespace Throne.Domain.Instructions;

public static class InstructionKindNames
{
    public const string Common = "common";
    public const string Interview = "interview";
    public const string Work = "work";
    public const string Dream = "dream";
    public const string Fix = "fix";

    public static readonly IReadOnlyList<string> All =
    [
        Common,
        Interview,
        Work,
        Dream,
        Fix,
    ];

    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);
}
