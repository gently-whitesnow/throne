namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral agent axis for the embedded terminal. Holds the closed vendor /
/// effort sets, the curated per-vendor model whitelists and the native per-vendor
/// defaults. Curated lists are hardcoded here and updated by editing this file — there
/// is no free-text model entry on the launch surface.
/// </summary>
public static class TerminalAgentCatalog
{
    public const string VendorClaude = "claude";
    public const string VendorCodex = "codex";

    public const string EffortLow = "low";
    public const string EffortMedium = "medium";
    public const string EffortHigh = "high";
    public const string EffortXhigh = "xhigh";

    private static readonly HashSet<string> Vendors = new(StringComparer.Ordinal) { VendorClaude, VendorCodex };

    private static readonly HashSet<string> Efforts =
        new(StringComparer.Ordinal) { EffortLow, EffortMedium, EffortHigh, EffortXhigh };

    private static readonly IReadOnlyList<string> ClaudeModels = ["opus", "sonnet", "haiku"];
    private static readonly IReadOnlyList<string> CodexModels = ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"];

    public static bool IsKnownVendor(string vendor) =>
        !string.IsNullOrEmpty(vendor) && Vendors.Contains(vendor);

    public static bool IsKnownEffort(string effort) =>
        !string.IsNullOrEmpty(effort) && Efforts.Contains(effort);

    public static IReadOnlyList<string> ModelsFor(string vendor) => vendor switch
    {
        VendorClaude => ClaudeModels,
        VendorCodex => CodexModels,
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'."),
    };

    public static bool IsKnownModel(string vendor, string model) =>
        !string.IsNullOrEmpty(model) && ModelsFor(vendor).Contains(model, StringComparer.Ordinal);

    /// <summary>
    /// Native default effort per vendor: claude reasons high by default, codex medium.
    /// </summary>
    public static string DefaultEffortFor(string vendor) => vendor switch
    {
        VendorClaude => EffortHigh,
        VendorCodex => EffortMedium,
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'."),
    };

    /// <summary>Vendor used when neither the request nor settings pin one.</summary>
    public const string DefaultVendor = VendorClaude;
}
