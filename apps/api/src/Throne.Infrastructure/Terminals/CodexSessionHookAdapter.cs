using System.Text;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Codex flavour of <see cref="ISessionHookAdapter"/>. Codex has no flag to load an arbitrary
/// external config file (<c>--profile</c> reads only <c>$CODEX_HOME/&lt;name&gt;.config.toml</c>), so
/// the per-session hook is injected inline through <c>-c hooks.Stop=...</c> — a single argv token
/// whose value Codex parses as TOML. This keeps every byte out of the clone, out of the operator's
/// <c>$CODEX_HOME</c>, and away from auth, which a generated profile file or a <c>CODEX_HOME</c>
/// overlay could not.
///
/// <c>--dangerously-bypass-hook-trust</c> rides along: a freshly generated command hook is otherwise
/// untrusted and Codex would skip it (or block on interactive <c>/hooks</c> review), stranding the
/// unattended tmux flow. Trusting our own generated hook is safe — Throne authored it.
/// </summary>
public sealed class CodexSessionHookAdapter(SessionHookOptions options) : ISessionHookAdapter
{
    private const string BypassHookTrustFlag = "--dangerously-bypass-hook-trust";

    public string Vendor => TerminalAgentCatalog.VendorCodex;

    public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
        string intentId, string workspacePath, string mode, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(mode);

        // One `-c hooks.<event>=...` override per event: each targets a distinct leaf under `hooks`,
        // so Codex merges them rather than the second clobbering the first.
        var args = new List<string>();
        foreach (var hookEvent in TerminalHookEvents.All)
        {
            var command = TerminalHookCallback.CurlCommand(options.ApiBaseUrl, intentId, hookEvent, mode);
            args.Add("-c");
            args.Add(
                $"hooks.{hookEvent}=[{{hooks=[{{type=\"command\",command={TomlBasicString(command)},timeout=10}}]}}]");
        }

        args.Add(BypassHookTrustFlag);
        return Task.FromResult<IReadOnlyList<string>>(args);
    }

    private static string TomlBasicString(string value)
    {
        var sb = new StringBuilder(value.Length + 2);
        sb.Append('"');
        foreach (var ch in value)
        {
            switch (ch)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(ch); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }
}
