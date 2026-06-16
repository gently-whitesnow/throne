namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral capability descriptor for one embedded-terminal vendor. Holds the
/// vendor token, display label, the static curated model whitelist (for
/// <see cref="ModelSource"/> = <c>static</c>; empty for <c>local</c>), whether the vendor
/// exposes a reasoning-effort axis (with its native default), the provenance of the model
/// list, and the spawn-argv builder.
/// <see cref="TerminalAgentCatalog"/> owns the closed set of descriptors; the resolver and
/// the spawn command read everything vendor-specific from here instead of switching on the
/// vendor token. For <c>local</c>-sourced vendors the live model list is materialised at
/// catalog-projection time from the operator's local OpenAI-compatible endpoint, not from
/// <see cref="Models"/>.
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
    /// <summary>
    /// Static native default model = first entry of <see cref="Models"/>. Null when the
    /// list is empty (only possible for <see cref="ModelSource"/> = <c>local</c>; that
    /// branch falls back to whatever the live endpoint advertises first).
    /// </summary>
    public string? DefaultModel => Models.Count == 0 ? null : Models[0];

    /// <summary>Whether <paramref name="model"/> is in this vendor's static whitelist.</summary>
    public bool HasModel(string model) =>
        !string.IsNullOrEmpty(model) && Models.Contains(model, StringComparer.Ordinal);
}
