namespace Throne.Domain.PromptParts;

/// <summary>
/// Role a <see cref="PromptPart"/> plays in a given embedded mode. Absence of a role
/// for a mode means the part is unavailable there.
/// </summary>
public static class PromptPartRoleNames
{
    /// <summary>Always included; cannot be toggled off by the operator.</summary>
    public const string Mandatory = "mandatory";

    /// <summary>Pre-selected by default; the operator may toggle it off.</summary>
    public const string DefaultOn = "default_on";

    /// <summary>Available but off by default; the operator may toggle it on.</summary>
    public const string DefaultOff = "default_off";

    public static readonly IReadOnlyList<string> All = [Mandatory, DefaultOn, DefaultOff];

    public static bool IsKnown(string role) => All.Contains(role, StringComparer.Ordinal);
}
