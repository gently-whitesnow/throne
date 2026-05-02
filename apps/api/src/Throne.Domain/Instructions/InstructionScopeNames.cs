namespace Throne.Domain.Instructions;

public static class InstructionScopeNames
{
    public const string System = "system";
    public const string User = "user";

    public static readonly IReadOnlyList<string> All = [System, User];

    public static bool IsKnown(string scope) => All.Contains(scope, StringComparer.Ordinal);
}
