namespace Throne.Domain.Instructions;

public static class InstructionKindNames
{
    public const string Common = "common";
    public const string Interview = "interview";
    public const string LightWork = "light_work";
    public const string NewProject = "new_project";

    public static readonly IReadOnlyList<string> All =
    [
        Common,
        Interview,
        LightWork,
        NewProject,
    ];

    public static bool IsKnown(string kind) => All.Contains(kind, StringComparer.Ordinal);
}
