namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral capability descriptor for one embedded-terminal vendor. Holds the
/// vendor token, display label, curated model whitelist, whether the vendor exposes a
/// reasoning-effort axis (with its native default), the provenance of the model list, and
/// the spawn-argv builder.
/// <see cref="TerminalAgentCatalog"/> owns the closed set of descriptors; the resolver and
/// the spawn command read everything vendor-specific from here instead of switching on the
/// vendor token.
///
/// Invariant: <see cref="SupportsEffort"/> ⇔ <see cref="DefaultEffort"/> is non-null and
/// <see cref="Efforts"/> is non-empty. A vendor without an effort axis carries a null
/// default, an empty effort list, and its <see cref="BuildBaseArgs"/> must not emit any
/// effort flag (the resolved <see cref="TerminalLaunchOptions.Effort"/> is null for it).
/// </summary>
public sealed record TerminalVendorDescriptor(
    string Vendor,
    string Label,
    IReadOnlyList<string> Models,
    bool SupportsEffort,
    IReadOnlyList<string> Efforts,
    string? DefaultEffort,
    string ModelSource,
    Func<TerminalLaunchOptions, IReadOnlyList<string>> BuildBaseArgs)
{
    /// <summary>Native default model = first entry of the curated list.</summary>
    public string DefaultModel => Models[0];

    /// <summary>Whether <paramref name="model"/> is in this vendor's curated whitelist.</summary>
    public bool HasModel(string model) =>
        !string.IsNullOrEmpty(model) && Models.Contains(model, StringComparer.Ordinal);
}
