using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Naming + on-disk location of the per-session Codex profile that carries the assembled rules
/// block as <c>developer_instructions</c>. Codex has no flag to read developer-instructions from an
/// arbitrary file, and the block is multi-KB so it cannot ride the tmux spawn argv inline; its only
/// file-backed channel is a <c>-p &lt;name&gt;</c> profile, which Codex resolves to
/// <c>$CODEX_HOME/&lt;name&gt;.config.toml</c>. The name is deterministic per intent (one file,
/// overwritten on re-run) and matches the tmux session name; <see cref="CodexSessionHookAdapter"/>
/// writes it and reaps it on intent-done cleanup.
/// </summary>
internal static class CodexSessionProfile
{
    /// <summary>
    /// Codex's config home — honours <c>$CODEX_HOME</c> (what the spawned <c>codex</c> reads at
    /// <c>-p</c> resolution) and falls back to <c>~/.codex</c>.
    /// </summary>
    public static string ResolveHome() =>
        Environment.GetEnvironmentVariable("CODEX_HOME") is { Length: > 0 } home
            ? home
            : WorkspacePathExpansion.ExpandHome("~/.codex");

    public static string Name(string intentId) => TmuxSessionName.Prefix + intentId;

    public static string PathFor(string home, string intentId) =>
        Path.Combine(home, Name(intentId) + ".config.toml");
}
