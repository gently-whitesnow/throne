namespace Throne.Application.Terminals;

/// <summary>
/// Stable wire tokens for the provider-neutral agent axis: vendor names, the closed effort set,
/// model-source provenance and the default vendor. The per-vendor
/// <see cref="TerminalVendorDescriptor"/>s live in <see cref="TerminalVendorDescriptors"/> and are
/// resolved at runtime through the DI-registered <see cref="ITerminalVendorCatalog"/> (ADR-0045) —
/// not from a static list here. The effort set is provider-neutral and closed, so it stays a
/// constant on this token holder rather than a per-vendor extension point.
/// </summary>
public static class TerminalAgentCatalog
{
    public const string VendorClaude = "claude";
    public const string VendorCodex = "codex";
    public const string VendorOpencode = "opencode";

    public const string EffortLow = "low";
    public const string EffortMedium = "medium";
    public const string EffortHigh = "high";
    public const string EffortXhigh = "xhigh";

    /// <summary>Curated model list is hardcoded in the descriptor (no dynamic discovery).</summary>
    public const string ModelSourceStatic = "static";

    /// <summary>
    /// Model list is materialised at projection time from the operator's local OpenAI-compatible
    /// endpoint (<c>Throne:LocalModel:BaseUrl</c>, probed via <c>GET /v1/models</c>). Used by
    /// <see cref="VendorOpencode"/>; the static <see cref="TerminalVendorDescriptor.Models"/>
    /// list on a <c>local</c> descriptor is empty by design.
    /// </summary>
    public const string ModelSourceLocal = "local";

    /// <summary>
    /// OpenCode provider id materialised in the generated <c>opencode.json</c> and prefixed onto
    /// the <c>--model</c> flag (<c>throne-local/&lt;modelId&gt;</c>). Public so the resolver, the
    /// session-hook adapter, and tests all spell it identically.
    /// </summary>
    public const string OpencodeProviderId = "throne-local";

    /// <summary>Vendor used when neither the request nor settings pin one.</summary>
    public const string DefaultVendor = VendorClaude;

    /// <summary>Closed effort set, ordered low → xhigh; shared across effort-capable vendors.</summary>
    public static readonly IReadOnlyList<string> SharedEfforts =
        [EffortLow, EffortMedium, EffortHigh, EffortXhigh];

    private static readonly HashSet<string> KnownEfforts =
        new(SharedEfforts, StringComparer.Ordinal);

    public static bool IsKnownEffort(string effort) =>
        !string.IsNullOrEmpty(effort) && KnownEfforts.Contains(effort);
}
