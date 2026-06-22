namespace Throne.Domain.Capabilities;

/// <summary>
/// Closed set of capability keys persisted in the <c>Capabilities</c> singleton.
/// Slice 2 ships three active keys (<c>repositories</c>, <c>terminal</c>,
/// <c>vscode</c>); <c>jira</c> and <c>gitlab</c> are reserved for later slices
/// and are valid storage targets even before any UI ships, so an early enable
/// from a config tool does not blow up the aggregate.
/// </summary>
public static class CapabilityNames
{
    public const string Repositories = "repositories";
    public const string Terminal = "terminal";
    public const string Vscode = "vscode";
    public const string Jira = "jira";
    public const string Gitlab = "gitlab";
    public const string Opencode = "opencode";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        Repositories,
        Terminal,
        Vscode,
        Jira,
        Gitlab,
        Opencode,
    };

    /// <summary>
    /// Essential capabilities for Throne's embedded-only contour (git via <see cref="Repositories"/>,
    /// agent terminal via <see cref="Terminal"/>). These are detection-ready: an installed,
    /// authenticated prerequisite is treated as enabled without an explicit opt-in toggle —
    /// ADR-0026 § 9 amendment, rationale embedded-only. Optional features (`vscode`, `gitlab`,
    /// `opencode`) keep the original explicit opt-in semantics.
    /// </summary>
    private static readonly HashSet<string> EssentialNames = new(StringComparer.Ordinal)
    {
        Repositories,
        Terminal,
    };

    public static bool IsKnown(string name) =>
        !string.IsNullOrEmpty(name) && Known.Contains(name);

    public static bool IsEssential(string name) =>
        !string.IsNullOrEmpty(name) && EssentialNames.Contains(name);
}
