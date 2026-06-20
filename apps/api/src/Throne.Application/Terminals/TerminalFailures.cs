using Throne.Application.Errors;

namespace Throne.Application.Terminals;

/// <summary>
/// Centralised <see cref="ApiException"/> factory for terminal/Run pre-flight failures.
/// Keeps controllers + orchestrator free of error-payload boilerplate.
/// </summary>
internal static class TerminalFailures
{
    public static ApiException CapabilityDisabled(string capability) =>
        new(
            ErrorCodes.CapabilityDisabled,
            $"Capability '{capability}' is disabled — enable it in /settings before retrying.",
            new Dictionary<string, object?> { ["capability"] = capability });

    public static ApiException ModeInvalid(string mode) =>
        new(
            ErrorCodes.TerminalModeInvalid,
            $"Unknown terminal run mode '{mode}'. Allowed: work | interview | review | dream | free.",
            new Dictionary<string, object?> { ["mode"] = mode });

    public static ApiException ReviewRequiresPullRequest(string mode, int count) =>
        new(
            ErrorCodes.ValidationFailed,
            count == 0
                ? "Review mode requires an attached pull request on the intent."
                : "Review mode cannot choose between multiple attached pull requests on the same intent.",
            new Dictionary<string, object?>
            {
                ["mode"] = mode,
                ["attached_pull_requests"] = count,
            });

    public static ApiException ReviewPullRequestNotAttached(
        string mode,
        string bindingId,
        IReadOnlyList<string> attachedBindingIds) =>
        new(
            ErrorCodes.ValidationFailed,
            "Review mode selected pull request is not attached to the intent.",
            new Dictionary<string, object?>
            {
                ["mode"] = mode,
                ["binding_id"] = bindingId,
                ["attached_binding_ids"] = attachedBindingIds,
            });

    public static ApiException VendorInvalid(string vendor) =>
        new(
            ErrorCodes.TerminalArgsInvalid,
            $"Unknown terminal vendor '{vendor}'. Allowed: {string.Join(" | ", TerminalAgentCatalog.Descriptors.Select(d => d.Vendor))}.",
            new Dictionary<string, object?> { ["vendor"] = vendor });

    public static ApiException ModelInvalid(string vendor, string model) =>
        new(
            ErrorCodes.TerminalArgsInvalid,
            BuildModelInvalidDetail(vendor, model),
            new Dictionary<string, object?> { ["vendor"] = vendor, ["model"] = model });

    private static string BuildModelInvalidDetail(string vendor, string model)
    {
        var descriptor = TerminalAgentCatalog.DescriptorFor(vendor);
        // For local-sourced vendors the static `Models` list is empty by design — the live list
        // comes from the operator's local endpoint, so phrase the error around that channel
        // instead of listing nothing as the allowed set.
        return descriptor.ModelSource == TerminalAgentCatalog.ModelSourceLocal
            ? $"Model '{model}' is not advertised by the local OpenAI-compatible endpoint for vendor '{vendor}'. Configure Throne:LocalModel:BaseUrl and verify GET /v1/models."
            : $"Model '{model}' is not in the curated whitelist for vendor '{vendor}'. Allowed: {string.Join(" | ", descriptor.Models)}.";
    }

    public static ApiException EffortInvalid(string effort) =>
        new(
            ErrorCodes.TerminalArgsInvalid,
            $"Unknown reasoning effort '{effort}'. Allowed: low | medium | high | xhigh.",
            new Dictionary<string, object?> { ["effort"] = effort });

    public static ApiException SessionAlreadyRunning(string intentId, string sessionName) =>
        new(
            ErrorCodes.TerminalSessionAlreadyRunning,
            $"tmux session '{sessionName}' for intent '{intentId}' is already running — use /restart instead.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["session_name"] = sessionName,
            });

    public static ApiException SpawnFailed(string intentId, string sessionName, string? detail) =>
        new(
            ErrorCodes.TerminalSpawnFailed,
            detail ?? $"tmux refused to spawn session '{sessionName}'.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["session_name"] = sessionName,
            });

    public static ApiException TuiReadinessTimeout(
        string intentId,
        string vendor,
        int waitedMilliseconds,
        int captures,
        string? lastSnapshot) =>
        new(
            ErrorCodes.TerminalTuiReadinessTimeout,
            $"Vendor '{vendor}' TUI for intent '{intentId}' did not render a ready composer within {waitedMilliseconds} ms ({captures} captures). User prompt not delivered — restart the run.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["vendor"] = vendor,
                ["waited_milliseconds"] = waitedMilliseconds,
                ["captures"] = captures,
                ["last_snapshot_excerpt"] = Excerpt(lastSnapshot),
            });

    public static ApiException InitialPromptSubmitFailed(
        string intentId,
        string vendor,
        string detail) =>
        new(
            ErrorCodes.TerminalInitialPromptSubmitFailed,
            $"Vendor '{vendor}' failed to submit the initial prompt for intent '{intentId}': {detail}",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["vendor"] = vendor,
                ["detail"] = detail,
            });

    // Surface only the tail of the captured pane — full snapshots can be multi-KB and the tail
    // is where the input row would be. Keeps the Problem Details payload bounded.
    private static string Excerpt(string? snapshot)
    {
        if (string.IsNullOrEmpty(snapshot))
        {
            return string.Empty;
        }
        const int max = 512;
        return snapshot.Length <= max ? snapshot : snapshot[^max..];
    }

    public static ApiException CloneWaitTimeout(
        string intentId,
        int waitedSeconds,
        IReadOnlyList<string> pendingBindings) =>
        new(
            ErrorCodes.TerminalCloneWaitTimeout,
            $"Pre-flight gave up waiting for clones after {waitedSeconds}s; bindings still in progress: {string.Join(", ", pendingBindings)}.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["waited_seconds"] = waitedSeconds,
                ["pending_bindings"] = pendingBindings,
            });
}
