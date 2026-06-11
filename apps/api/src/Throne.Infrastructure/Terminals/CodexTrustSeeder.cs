using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Seeds per-directory trust in codex's <c>~/.codex/config.toml</c> so spawning into a new intent
/// workspace skips the startup "do you trust this folder?" onboarding and lets codex run in its
/// Auto profile (sandbox <c>workspace-write</c> + approval <c>on-failure</c>). The TOML merge
/// lives in <see cref="CodexTrustDocument"/>; the read/atomic-write in <see cref="TrustConfigFile"/>.
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
