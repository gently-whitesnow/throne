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

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        Repositories,
        Terminal,
        Vscode,
        Jira,
        Gitlab,
    };

    public static bool IsKnown(string name) =>
        !string.IsNullOrEmpty(name) && Known.Contains(name);
}
