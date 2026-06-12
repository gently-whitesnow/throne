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
/// codex launches with <c>--dangerously-bypass-approvals-and-sandbox</c> (alias <c>--yolo</c>):
/// the operator presses run and walks away, so mid-task approval prompts on routine work
/// (git fetch / branch from a remote ref, dependency install — all blocked by the default
/// workspace-write sandbox's no-network policy) would strand the session. This is codex's
/// "no approvals, no sandbox" profile; granular per-command policy is a deliberate future layer.
/// </summary>
public static class AgentSpawnCommand
{
    /// <param name="prompt">Boot prompt. Appended as a positional arg unless <paramref name="isFree"/>.</param>
    /// <param name="isFree">
    /// Free mode boots the agent bare (no positional prompt — it would auto-run) and the
    /// operator's editable prompt is pre-typed afterwards. Model/effort flags still apply.
    /// </param>
    /// <param name="hookArgs">
    /// Per-session hook injection tokens from the vendor's <see cref="ISessionHookAdapter"/>,
    /// inserted after the base model/effort flags and before the positional prompt. Vendor-neutral
    /// here — the adapter owns what they mean (Claude <c>--settings &lt;file&gt;</c>, Codex inline
    /// <c>-c hooks...</c> + bypass flag).
    /// </param>
    public static AgentSpawnInvocation Build(
        TerminalLaunchOptions options,
        string prompt,
        bool isFree,
        IReadOnlyList<string>? hookArgs = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var args = options.Vendor switch
        {
            TerminalAgentCatalog.VendorClaude =>
                new List<string> { "--model", options.Model, "--effort", options.Effort },
            TerminalAgentCatalog.VendorCodex =>
                new List<string>
                {
                    "-m", options.Model,
                    "-c", $"model_reasoning_effort={options.Effort}",
                    "--dangerously-bypass-approvals-and-sandbox",
                },
            _ => throw new ArgumentOutOfRangeException(
                nameof(options), $"Unknown terminal vendor '{options.Vendor}'."),
        };

        if (hookArgs is { Count: > 0 })
        {
            args.AddRange(hookArgs);
        }

        if (!isFree)
        {
            args.Add(prompt);
        }

        return new AgentSpawnInvocation(options.Vendor, args);
    }
}
