using Throne.Application.Terminals;
using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Seeds per-directory trust in claude's <c>~/.claude.json</c> so spawning into a new intent
/// workspace skips the interactive trust prompt. The JSON merge lives in
/// <see cref="ClaudeTrustDocument"/>; the read/atomic-write in <see cref="TrustConfigFile"/>.
/// </summary>
internal sealed class ClaudeTrustSeeder : IWorkspaceTrustSeeder
{
    // claude persists per-project state (incl. the accepted-trust flag) here.
    private static readonly string ConfigPath =
        WorkspacePathExpansion.ExpandHome("~/.claude.json");

    public string Vendor => TerminalAgentCatalog.VendorClaude;

    public void Seed(string workspacePath) =>
        TrustConfigFile.Seed(
            ConfigPath,
            existing => ClaudeTrustDocument.WithTrustedWorkspace(existing, workspacePath));

    public void RemoveTrustedUnder(string directoryPrefix) =>
        TrustConfigFile.Seed(
            ConfigPath,
            existing => ClaudeTrustDocument.WithoutTrustedWorkspacesUnder(existing, directoryPrefix));
}
