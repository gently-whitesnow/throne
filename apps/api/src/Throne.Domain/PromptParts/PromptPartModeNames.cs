namespace Throne.Domain.PromptParts;

/// <summary>
/// Run modes a <see cref="PromptPart"/> role can target (ADR-0036): the embedded composition
/// modes <c>interview</c>/<c>work</c>/<c>review</c>/<c>dream</c>/<c>free</c>. Absence of a role
/// for a mode means the part is unavailable there.
/// </summary>
public static class PromptPartModeNames
{
    public const string Interview = "interview";
    public const string Work = "work";
    public const string Review = "review";
    public const string Dream = "dream";
    public const string Free = "free";

    public static readonly IReadOnlyList<string> All = [Interview, Work, Review, Dream, Free];

    public static bool IsKnown(string mode) => All.Contains(mode, StringComparer.Ordinal);
}
