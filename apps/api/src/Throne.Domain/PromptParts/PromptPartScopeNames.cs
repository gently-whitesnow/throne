namespace Throne.Domain.PromptParts;

/// <summary>
/// Scope of a <see cref="PromptPart"/>: <c>system</c> parts are seeded from the skill
/// manifest and read-only to operators; <c>user</c> parts are operator-authored.
/// </summary>
public static class PromptPartScopeNames
{
    public const string System = "system";
    public const string User = "user";

    public static readonly IReadOnlyList<string> All = [System, User];

    public static bool IsKnown(string scope) => All.Contains(scope, StringComparer.Ordinal);
}
