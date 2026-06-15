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
            $"Unknown terminal run mode '{mode}'. Allowed: work | interview | dream.",
            new Dictionary<string, object?> { ["mode"] = mode });

    public static ApiException VendorInvalid(string vendor) =>
        new(
            ErrorCodes.TerminalArgsInvalid,
            $"Unknown terminal vendor '{vendor}'. Allowed: claude | codex.",
            new Dictionary<string, object?> { ["vendor"] = vendor });

    public static ApiException ModelInvalid(string vendor, string model) =>
        new(
            ErrorCodes.TerminalArgsInvalid,
            $"Model '{model}' is not in the curated whitelist for vendor '{vendor}'. Allowed: {string.Join(" | ", TerminalAgentCatalog.ModelsFor(vendor))}.",
            new Dictionary<string, object?> { ["vendor"] = vendor, ["model"] = model });

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
