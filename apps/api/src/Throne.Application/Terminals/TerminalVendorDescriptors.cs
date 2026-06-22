namespace Throne.Application.Terminals;

/// <summary>
/// The concrete <see cref="TerminalVendorDescriptor"/> definitions, one per supported embedded
/// terminal vendor. Each is registered individually into DI (see Application composition root)
/// and surfaced at runtime through <see cref="ITerminalVendorCatalog"/> — the same
/// port → DI fan-out → registry idiom as git providers and capability probes (ADR-0045).
/// Adding a vendor is a new descriptor here plus one DI registration line; no central list or
/// switch is edited.
/// </summary>
public static class TerminalVendorDescriptors
{
    public static readonly TerminalVendorDescriptor Claude = new(
        Vendor: TerminalAgentCatalog.VendorClaude,
        Label: "Claude",
        Models: ["opus", "sonnet", "haiku"],
        SupportsEffort: true,
        Efforts: TerminalAgentCatalog.SharedEfforts,
        DefaultEffort: TerminalAgentCatalog.EffortHigh,
        ModelSource: TerminalAgentCatalog.ModelSourceStatic,
        BuildBaseArgs: static options => ["--model", options.Model, "--effort", options.Effort!],
        SupportsNativeHotAttach: true);

    public static readonly TerminalVendorDescriptor Codex = new(
        Vendor: TerminalAgentCatalog.VendorCodex,
        Label: "Codex",
        Models: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"],
        SupportsEffort: true,
        Efforts: TerminalAgentCatalog.SharedEfforts,
        DefaultEffort: TerminalAgentCatalog.EffortMedium,
        ModelSource: TerminalAgentCatalog.ModelSourceStatic,
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
        ],
        SupportsNativeHotAttach: true);

    // OpenCode reads its top-level provider/model from the workspace-local `opencode.json` that
    // the session-hook adapter writes (npm = "@ai-sdk/openai-compatible", baseURL = local /v1
    // endpoint, models map = live discovery). The spawn argv carries no model/effort flag: the
    // agent loop runs in a shared `opencode serve`, not in this pane, and the model is pinned
    // server-side on the prompt (`prompt_async` model={providerID,modelID}) by the session-hook
    // adapter. The pane only runs `opencode attach <url> --session <id>` — the adapter supplies
    // that whole argv as prepared args, so BuildBaseArgs is empty. No effort axis either:
    // OpenCode does not surface reasoning-effort tiers (SupportsEffort=false).
    public static readonly TerminalVendorDescriptor Opencode = new(
        Vendor: TerminalAgentCatalog.VendorOpencode,
        Label: "OpenCode",
        Models: [],
        SupportsEffort: false,
        Efforts: [],
        DefaultEffort: null,
        ModelSource: TerminalAgentCatalog.ModelSourceLocal,
        BuildBaseArgs: static _ => [],
        // Pinned to local models (Throne:LocalModel, ADR-0042); local models are temporarily
        // unsupported, so opencode is surfaced as `in_development` — visible but not launchable
        // and excluded from the readiness check. Full rework tracked as a child intent.
        InDevelopment: true,
        // OpenCode TUI needs mouse reporting on in the tmux pane for scroll/select to work.
        EnableMouse: true);
}
