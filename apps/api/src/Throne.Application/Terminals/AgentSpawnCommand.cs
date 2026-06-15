namespace Throne.Application.Terminals;

/// <summary>
/// Resolved, validated launch axis for one terminal session. Vendor / model / effort are
/// already defaulted and whitelist-checked — <see cref="AgentSpawnCommand"/> trusts them.
/// <see cref="Effort"/> is null for a vendor whose descriptor declares no effort axis.
/// </summary>
public sealed record TerminalLaunchOptions(string Vendor, string Model, string? Effort);

/// <summary>
/// Pre-split spawn invocation handed to <see cref="ITmuxSessionManager.SpawnAsync"/>:
/// <c>tmux new -ADs throne-{id} -- {Command} {Arguments...}</c>.
/// </summary>
public sealed record AgentSpawnInvocation(string Command, IReadOnlyList<string> Arguments);

/// <summary>
/// Assembles the spawn argv: the vendor's base flags (from its
/// <see cref="TerminalVendorDescriptor.BuildBaseArgs"/>) plus the per-session injection
/// tokens. Vendors configure model and effort entirely through per-launch CLI flags — no
/// vendor config files are touched. tmux passes the argv straight to <c>execvp</c>, so flag
/// values are raw tokens (no shell quoting): the codex docs show
/// <c>-c model_reasoning_effort="high"</c> for a shell, here it is the unquoted
/// <c>model_reasoning_effort=high</c> token.
///
/// Upfront context (ADR-0034): the embedded contour injects the assembled rules block and the
/// task as the session's starting context instead of asking the agent to read a bundle. Neither
/// rides on this argv — both are multi-KB on real workloads and tmux packs the whole spawn argv
/// into one ~16 KB imsg (<c>command too long</c> above that). The rules block is materialised
/// to a per-session file by the vendor's <see cref="ISessionHookAdapter"/>, which hands back
/// small reference tokens (<c>--append-system-prompt-file</c> for Claude, a <c>-p</c> profile
/// for Codex) via <paramref name="preparedArgs"/>. The user task is pasted into the live pane
/// after spawn by <see cref="ITmuxSessionManager.PasteFileAsSubmittedPromptAsync"/> — also from
/// a file, server-side, so the argv stays small regardless of prompt size.
/// </summary>
public static class AgentSpawnCommand
{
    /// <param name="preparedArgs">
    /// Per-session injection tokens from the vendor's <see cref="ISessionHookAdapter"/> (hooks +
    /// the file-backed system-context reference), appended after the model/effort flags.
    /// Vendor-neutral here — the adapter owns what they mean.
    /// </param>
    public static AgentSpawnInvocation Build(
        TerminalLaunchOptions options,
        IReadOnlyList<string>? preparedArgs = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var descriptor = TerminalAgentCatalog.DescriptorFor(options.Vendor);
        var args = new List<string>(descriptor.BuildBaseArgs(options));

        if (preparedArgs is { Count: > 0 })
        {
            args.AddRange(preparedArgs);
        }

        return new AgentSpawnInvocation(options.Vendor, args);
    }
}
