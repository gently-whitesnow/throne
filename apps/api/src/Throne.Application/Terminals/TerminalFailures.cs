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
            TerminalErrorCodes.ModeInvalid,
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

    public static ApiException VendorInvalid(ITerminalVendorCatalog catalog, string vendor) =>
        new(
            TerminalErrorCodes.ArgsInvalid,
            $"Unknown terminal vendor '{vendor}'. Allowed: {string.Join(" | ", catalog.Descriptors.Select(d => d.Vendor))}.",
            new Dictionary<string, object?> { ["vendor"] = vendor });

    public static ApiException ModelInvalid(TerminalVendorDescriptor descriptor, string model) =>
        new(
            TerminalErrorCodes.ArgsInvalid,
            BuildModelInvalidDetail(descriptor, model),
            new Dictionary<string, object?> { ["vendor"] = descriptor.Vendor, ["model"] = model });

    private static string BuildModelInvalidDetail(TerminalVendorDescriptor descriptor, string model) =>
        // For local-sourced vendors the static `Models` list is empty by design — the live list
        // comes from the operator's local endpoint, so phrase the error around that channel
        // instead of listing nothing as the allowed set.
        descriptor.ModelSource == TerminalAgentCatalog.ModelSourceLocal
            ? $"Model '{model}' is not advertised by the local OpenAI-compatible endpoint for vendor '{descriptor.Vendor}'. Configure Throne:LocalModel:BaseUrl and verify GET /v1/models."
            : $"Model '{model}' is not in the curated whitelist for vendor '{descriptor.Vendor}'. Allowed: {string.Join(" | ", descriptor.Models)}.";

    public static ApiException EffortInvalid(string effort) =>
        new(
            TerminalErrorCodes.ArgsInvalid,
            $"Unknown reasoning effort '{effort}'. Allowed: low | medium | high | xhigh.",
            new Dictionary<string, object?> { ["effort"] = effort });

    public static ApiException SessionAlreadyRunning(string intentId, string sessionName) =>
        new(
            TerminalErrorCodes.SessionAlreadyRunning,
            $"tmux session '{sessionName}' for intent '{intentId}' is already running.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["session_name"] = sessionName,
            });

    public static ApiException SpawnFailed(string intentId, string sessionName, string? detail) =>
        new(
            TerminalErrorCodes.SpawnFailed,
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
            TerminalErrorCodes.TuiReadinessTimeout,
            $"Vendor '{vendor}' TUI for intent '{intentId}' did not render a ready composer within {waitedMilliseconds} ms ({captures} captures). User prompt not delivered — restart the run.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["vendor"] = vendor,
                ["waited_milliseconds"] = waitedMilliseconds,
                ["captures"] = captures,
                ["last_snapshot_excerpt"] = Excerpt(lastSnapshot),
            });

    public static ApiException PromptSubmitNotConfirmed(
        string intentId,
        string vendor,
        int retries,
        int confirmTimeoutMilliseconds,
        string? lastSnapshot) =>
        new(
            TerminalErrorCodes.PromptSubmitNotConfirmed,
            $"Vendor '{vendor}' did not acknowledge prompt submit for intent '{intentId}' after {retries} retry(ies) / {confirmTimeoutMilliseconds} ms per poll — the pasted prompt is sitting in the composer unsubmitted. Restart the session.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["vendor"] = vendor,
                ["retries"] = retries,
                ["confirm_timeout_milliseconds"] = confirmTimeoutMilliseconds,
                ["last_snapshot_excerpt"] = Excerpt(lastSnapshot),
            });

    public static ApiException InitialPromptSubmitFailed(
        string intentId,
        string vendor,
        string detail) =>
        new(
            TerminalErrorCodes.InitialPromptSubmitFailed,
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
            TerminalErrorCodes.CloneWaitTimeout,
            $"Pre-flight gave up waiting for clones after {waitedSeconds}s; bindings still in progress: {string.Join(", ", pendingBindings)}.",
            new Dictionary<string, object?>
            {
                ["intent_id"] = intentId,
                ["waited_seconds"] = waitedSeconds,
                ["pending_bindings"] = pendingBindings,
            });
}
