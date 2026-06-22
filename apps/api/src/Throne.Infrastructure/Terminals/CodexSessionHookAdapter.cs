using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Codex flavour of <see cref="ISessionHookAdapter"/>. Two per-session concerns, handled
/// differently because of what fits where:
///
/// <para>Hooks stay inline through <c>-c hooks.&lt;event&gt;=...</c> tokens (small) — Codex has no
/// flag to load an arbitrary external config file, and an inline override keeps every hook byte out
/// of the clone and out of <c>$CODEX_HOME</c>. <c>--dangerously-bypass-hook-trust</c> rides along so
/// a freshly generated command hook is not skipped or blocked on interactive review.</para>
///
/// <para>The assembled rules block does NOT fit inline: it is multi-KB and the whole spawn argv is
/// packed into one ~16 KB tmux imsg (<c>command too long</c> above that). Rules still go through a
/// <c>-p &lt;name&gt;</c> profile under <c>$CODEX_HOME</c>, while session skills are staged natively
/// into <c>.agents/skills/</c> by <see cref="ISessionSkillMaterializer"/>.</para>
/// </summary>
public sealed class CodexSessionHookAdapter(
    SessionHookOptions options,
    ISessionSkillMaterializer skillMaterializer,
    string codexHome) : ISessionHookAdapter
{
    private const string BypassHookTrustFlag = "--dangerously-bypass-hook-trust";

    public string Vendor => TerminalAgentCatalog.VendorCodex;

    public async Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId,
        string workspacePath,
        string mode,
        string? systemPrompt,
        IReadOnlyList<SessionSkillPackage> skillPackages,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        // One `-c hooks.<event>=...` override per event: each targets a distinct leaf under `hooks`,
        // so Codex merges them rather than the second clobbering the first.
        var args = new List<string>();
        foreach (var binding in TerminalHookEvents.CodexBindings)
        {
            var command = TerminalHookCallback.CurlCommand(options.ApiBaseUrl, intentId, binding.Event, mode);
            args.Add("-c");
            args.Add(
                $"hooks.{binding.Event}=[{{hooks=[{{type=\"command\",command={CodexConfigValue.ToToml(command)},timeout=10}}]}}]");
        }

        args.Add(BypassHookTrustFlag);

        await skillMaterializer.MaterializeAsync(
            workspacePath, TerminalAgentCatalog.VendorCodex, skillPackages, ct);
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            await WriteProfileAsync(intentId, systemPrompt!, ct);
            args.Add("-p");
            args.Add(CodexSessionProfile.Name(intentId));
        }

        return args;
    }

    // Codex TUI also draws a box-drawing composer; its input row is `│ >`. Same reason as
    // the Claude adapter: the input-row glyph is the only point at which a paste can actually
    // land — the top border alone can appear during the boot splash before stdin is hooked up.
    public bool IsTuiReady(string paneSnapshot) =>
        !string.IsNullOrEmpty(paneSnapshot)
        && paneSnapshot.Contains("│ >", StringComparison.Ordinal);

    // Codex renders an `esc to interrupt` working footer while processing a submitted prompt,
    // matching the cross-vendor convention Claude uses. Pre-submit ready state never paints it,
    // so its presence is a positive confirmation that the trailing Enter was honoured.
    public bool IsPromptSubmitted(string paneSnapshot) =>
        !string.IsNullOrEmpty(paneSnapshot)
        && paneSnapshot.Contains("esc to interrupt", StringComparison.OrdinalIgnoreCase);

    public Task CleanupAsync(string intentId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        var profilePath = CodexSessionProfile.PathFor(codexHome, intentId);
        if (File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }

        return Task.CompletedTask;
    }

    private async Task WriteProfileAsync(string intentId, string systemPrompt, CancellationToken ct)
    {
        Directory.CreateDirectory(codexHome);
        var profilePath = CodexSessionProfile.PathFor(codexHome, intentId);
        // `-p` layers this file over the base config; the `-c model_reasoning_effort` / `-m` flags
        // still apply. The value is a TOML basic string — Codex parses the file as config.toml.
        await File.WriteAllTextAsync(
            profilePath,
            $"developer_instructions = {CodexConfigValue.ToToml(systemPrompt)}\n",
            ct);
    }
}
