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

    public const string EffortLow = "low";
    public const string EffortMedium = "medium";
    public const string EffortHigh = "high";
    public const string EffortXhigh = "xhigh";

    /// <summary>Vendor used when neither the request nor settings pin one.</summary>
    public const string DefaultVendor = VendorClaude;

    private static readonly HashSet<string> Efforts =
        new(StringComparer.Ordinal) { EffortLow, EffortMedium, EffortHigh, EffortXhigh };

    private static readonly TerminalVendorDescriptor Claude = new(
        Vendor: VendorClaude,
        Label: "Claude",
        Models: ["opus", "sonnet", "haiku"],
        SupportsEffort: true,
        DefaultEffort: EffortHigh,
        BuildBaseArgs: static options => ["--model", options.Model, "--effort", options.Effort!]);

    private static readonly TerminalVendorDescriptor Codex = new(
        Vendor: VendorCodex,
        Label: "Codex",
        Models: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"],
        SupportsEffort: true,
        DefaultEffort: EffortMedium,
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

    private static readonly Dictionary<string, TerminalVendorDescriptor> ByVendor =
        new(StringComparer.Ordinal)
        {
            [VendorClaude] = Claude,
            [VendorCodex] = Codex,
        };

    public static TerminalVendorDescriptor DescriptorFor(string vendor) =>
        ByVendor.TryGetValue(vendor, out var descriptor)
            ? descriptor
            : throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'.");

    public static bool IsKnownVendor(string vendor) =>
        !string.IsNullOrEmpty(vendor) && ByVendor.ContainsKey(vendor);

    public static bool IsKnownEffort(string effort) =>
        !string.IsNullOrEmpty(effort) && Efforts.Contains(effort);

    public static IReadOnlyList<string> ModelsFor(string vendor) => DescriptorFor(vendor).Models;

    public static bool IsKnownModel(string vendor, string model) =>
        !string.IsNullOrEmpty(model) && ModelsFor(vendor).Contains(model, StringComparer.Ordinal);
}
