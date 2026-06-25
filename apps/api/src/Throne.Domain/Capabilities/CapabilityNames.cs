namespace Throne.Domain.Capabilities;

/// <summary>
/// Closed set of carrier capability keys persisted in the <c>capabilities</c> singleton.
/// A capability exists only when a feature has multiple interchangeable providers and the
/// operator selects an active one (e.g. «Открыть в IDE» → VS Code vs Cursor). Plain
/// «фичи» whose availability is purely a function of probe detection are not capabilities.
/// </summary>
public static class CapabilityNames
{
    public const string OpenInIde = "open_in_ide";
    public const string OpenInTerminal = "open_in_terminal";

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        OpenInIde,
        OpenInTerminal,
    };

    public static bool IsKnown(string name) =>
        !string.IsNullOrEmpty(name) && Known.Contains(name);
}
