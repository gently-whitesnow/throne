namespace Throne.Domain.PromptParts;

/// <summary>
/// Run modes a <see cref="PromptPart"/> role can target (ADR-0036). Unifies the bundle
/// modes (<c>interview</c>/<c>work</c>/<c>dream</c>/<c>schema_map</c>) consumed by
/// <c>get_prompt_bundle</c> with the embedded composition modes
/// (<c>work</c>/<c>interview</c>/<c>free</c>). Absence of a role for a mode means the
/// part is unavailable there.
/// </summary>
public static class PromptPartModeNames
{
    public const string Interview = "interview";
    public const string Work = "work";
    public const string Dream = "dream";
    public const string SchemaMap = "schema_map";
    public const string Free = "free";

    public static readonly IReadOnlyList<string> All = [Interview, Work, Dream, SchemaMap, Free];

    public static bool IsKnown(string mode) => All.Contains(mode, StringComparer.Ordinal);
}
