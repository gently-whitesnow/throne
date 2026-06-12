namespace Throne.Application.Terminals;

/// <summary>
/// Resolved, validated launch axis for one terminal session. Vendor / model / effort are
/// already defaulted and whitelist-checked — <see cref="AgentSpawnCommand"/> trusts them.
/// </summary>
public sealed record TerminalLaunchOptions(string Vendor, string Model, string Effort);

/// <summary>
/// Pre-split spawn invocation handed to <see cref="ITmuxSessionManager.SpawnAsync"/>:
/// <c>tmux new -ADs throne-{id} -- {Command} {Arguments...}</c>.
/// </summary>
public sealed record AgentSpawnInvocation(string Command, IReadOnlyList<string> Arguments);

/// <summary>
/// Builds the per-vendor spawn argv. Vendors configure model and effort entirely through
/// per-launch CLI flags — no vendor config files are touched. tmux passes the argv straight
/// to <c>execvp</c>, so flag values are raw tokens (no shell quoting): the codex docs show
/// <c>-c model_reasoning_effort="high"</c> for a shell, here it is the unquoted
/// <c>model_reasoning_effort=high</c> token.
///
/// Upfront context (ADR-0034): the embedded contour injects the assembled rules block and the
/// task as the session's starting context instead of asking the agent to read a bundle. The task
/// is the positional initial user message; empty task drops its token so a bare boot stays bare.
/// The rules block does NOT travel on this argv — it is multi-KB and tmux packs the whole spawn
/// argv into one ~16 KB imsg (<c>command too long</c> above that). Instead the vendor's
/// <see cref="ISessionHookAdapter"/> materialises it to a per-session file and hands back small
/// reference tokens (<c>--append-system-prompt-file</c> for Claude, a <c>-p</c> profile for Codex)
/// that ride in via <paramref name="preparedArgs"/>.
///
/// codex launches with <c>--dangerously-bypass-approvals-and-sandbox</c> (alias <c>--yolo</c>):
/// the operator presses run and walks away, so mid-task approval prompts on routine work
/// (git fetch / branch from a remote ref, dependency install — all blocked by the default
/// workspace-write sandbox's no-network policy) would strand the session. This is codex's
/// "no approvals, no sandbox" profile; granular per-command policy is a deliberate future layer.
/// </summary>
public static class AgentSpawnCommand
{
    /// <param name="userPrompt">
    /// Task delivered as the positional initial user message. Null/blank → the agent boots
    /// without a pre-filled prompt (operator types into the live session themselves).
    /// </param>
    /// <param name="preparedArgs">
    /// Per-session injection tokens from the vendor's <see cref="ISessionHookAdapter"/> (hooks +
    /// the file-backed system-context reference), inserted after the model/effort flags and before
    /// the positional task. Vendor-neutral here — the adapter owns what they mean.
    /// </param>
    public static AgentSpawnInvocation Build(
        TerminalLaunchOptions options,
        string? userPrompt,
        IReadOnlyList<string>? preparedArgs = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var args = BaseArgs(options);

        if (preparedArgs is { Count: > 0 })
        {
            args.AddRange(preparedArgs);
        }

        if (!string.IsNullOrWhiteSpace(userPrompt))
        {
            args.Add(userPrompt);
        }

        return new AgentSpawnInvocation(options.Vendor, args);
    }

    private static List<string> BaseArgs(TerminalLaunchOptions options) => options.Vendor switch
    {
        TerminalAgentCatalog.VendorClaude =>
            ["--model", options.Model, "--effort", options.Effort],
        TerminalAgentCatalog.VendorCodex =>
            [
                "-m", options.Model,
                "-c", $"model_reasoning_effort={options.Effort}",
                "--dangerously-bypass-approvals-and-sandbox",
            ],
        _ => throw new ArgumentOutOfRangeException(
            nameof(options), $"Unknown terminal vendor '{options.Vendor}'."),
    };
}
