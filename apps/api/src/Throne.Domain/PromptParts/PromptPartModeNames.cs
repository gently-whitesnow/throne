namespace Throne.Domain.PromptParts;

/// <summary>
/// Embedded run modes a <see cref="PromptPart"/> role can target. These are the
/// pre-flight composition modes (ADR-0035), distinct from the MCP bundle modes
/// (<c>dream</c>/<c>schema_map</c>) that have no embedded part selection.
/// </summary>
public static class PromptPartModeNames
{
    public const string Work = "work";
    public const string Interview = "interview";
    public const string Free = "free";

    public static readonly IReadOnlyList<string> All = [Work, Interview, Free];

    public static bool IsKnown(string mode) => All.Contains(mode, StringComparer.Ordinal);
}
