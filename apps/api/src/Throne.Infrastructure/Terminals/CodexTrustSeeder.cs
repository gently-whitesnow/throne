using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Seeds per-directory trust in codex's <c>~/.codex/config.toml</c> so spawning into a new intent
/// workspace skips the startup "do you trust this folder?" onboarding prompt. Runtime autonomy
/// (no approvals, no sandbox) is granted separately by the spawn argv's
/// <c>--dangerously-bypass-approvals-and-sandbox</c> flag — see <c>AgentSpawnCommand</c>; this seed
/// only removes the one-time trust gate. The TOML merge lives in <see cref="CodexTrustDocument"/>;
/// the read/atomic-write in <see cref="TrustConfigFile"/>.
/// </summary>
internal sealed class CodexTrustSeeder : IWorkspaceTrustSeeder
{
    private static readonly string ConfigPath =
        WorkspacePathExpansion.ExpandHome("~/.codex/config.toml");

    public string Vendor => TerminalAgentCatalog.VendorCodex;

    public void Seed(string workspacePath) =>
        TrustConfigFile.Seed(
            ConfigPath,
            existing => CodexTrustDocument.WithTrustedWorkspace(existing, workspacePath));
}
