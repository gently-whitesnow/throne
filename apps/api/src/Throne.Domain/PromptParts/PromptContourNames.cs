namespace Throne.Domain.PromptParts;

/// <summary>
/// Execution contour a prompt bundle targets (ADR-0034). The two contours deliver context and
/// move intent status differently — <c>standalone</c> is the external MCP agent (explicit
/// <c>set_intent_status(ready_for_review)</c> at end of pass), <c>embedded</c> is the built-in
/// terminal on hooks (the Stop-hook parks into <c>awaiting_operator</c>; the agent only reports).
/// A bundle without a contour is contour-neutral and resolves for any request.
/// </summary>
public static class PromptContourNames
{
    public const string Standalone = "standalone";
    public const string Embedded = "embedded";

    public static readonly IReadOnlyList<string> All = [Standalone, Embedded];

    public static bool IsKnown(string contour) => All.Contains(contour, StringComparer.Ordinal);
}
