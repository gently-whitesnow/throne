namespace Throne.Application.Terminals;

/// <summary>
/// Provider-neutral agent axis for the embedded terminal. Holds the closed effort set and
/// the per-vendor <see cref="TerminalVendorDescriptor"/>s (label, curated model whitelist,
/// effort capability + native default, spawn-argv builder). Curated lists are hardcoded in
/// the descriptors and updated by editing this file — there is no free-text model entry on
/// the launch surface, and no new vendor is registered at runtime.
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

    // Closed effort set, ordered low → xhigh; shared across effort-capable vendors.
    private static readonly IReadOnlyList<string> SharedEfforts =
        [EffortLow, EffortMedium, EffortHigh, EffortXhigh];

    private static readonly HashSet<string> KnownEfforts =
        new(SharedEfforts, StringComparer.Ordinal);

    private static readonly TerminalVendorDescriptor Claude = new(
        Vendor: VendorClaude,
        Label: "Claude",
        Models: ["opus", "sonnet", "haiku"],
        SupportsEffort: true,
        Efforts: SharedEfforts,
        DefaultEffort: EffortHigh,
        ModelSource: ModelSourceStatic,
        BuildBaseArgs: static options => ["--model", options.Model, "--effort", options.Effort!]);

    private static readonly TerminalVendorDescriptor Codex = new(
        Vendor: VendorCodex,
        Label: "Codex",
        Models: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"],
        SupportsEffort: true,
        Efforts: SharedEfforts,
        DefaultEffort: EffortMedium,
        ModelSource: ModelSourceStatic,
        // codex launches with --dangerously-bypass-approvals-and-sandbox (alias --yolo): the
        // operator presses run and walks away, so mid-task approval prompts on routine work
        // (git fetch / branch from a remote ref, dependency install — all blocked by the
        // default workspace-write sandbox's no-network policy) would strand the session.
        // tmux passes the argv straight to execvp, so the -c value is a raw unquoted token.
        BuildBaseArgs: static options =>
        [
            "-m", options.Model,
            "-c", $"model_reasoning_effort={options.Effort}",
            "--dangerously-bypass-approvals-and-sandbox",
        ]);

    // OpenCode reads its top-level provider/model from the workspace-local `opencode.json` that
    // the session-hook adapter writes (npm = "@ai-sdk/openai-compatible", baseURL = local /v1
    // endpoint, models map = live discovery). The `--model` flag here merely pins the active
    // model for this launch in the form `<providerId>/<modelId>` — the form OpenCode's CLI
    // requires for selection. No effort axis: OpenCode does not surface reasoning-effort tiers,
    // so SupportsEffort=false and BuildBaseArgs emits no effort flag.
    private static readonly TerminalVendorDescriptor Opencode = new(
        Vendor: VendorOpencode,
        Label: "OpenCode",
        Models: [],
        SupportsEffort: false,
        Efforts: [],
        DefaultEffort: null,
        ModelSource: ModelSourceLocal,
        BuildBaseArgs: static options => ["--model", $"{OpencodeProviderId}/{options.Model}"],
        RequiredCapability: Throne.Domain.Capabilities.CapabilityNames.Opencode);

    /// <summary>Descriptors in catalog (display) order; drives the launch-surface dropdown.</summary>
    public static readonly IReadOnlyList<TerminalVendorDescriptor> Descriptors = [Claude, Codex, Opencode];

    private static readonly Dictionary<string, TerminalVendorDescriptor> ByVendor =
        new(StringComparer.Ordinal)
        {
            [VendorClaude] = Claude,
            [VendorCodex] = Codex,
            [VendorOpencode] = Opencode,
        };

    public static TerminalVendorDescriptor DescriptorFor(string vendor) =>
        ByVendor.TryGetValue(vendor, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'.");

    public static bool IsKnownVendor(string vendor) =>
        !string.IsNullOrEmpty(vendor) && ByVendor.ContainsKey(vendor);

    public static bool IsKnownEffort(string effort) =>
        !string.IsNullOrEmpty(effort) && KnownEfforts.Contains(effort);
}
