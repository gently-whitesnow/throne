using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Infrastructure implementation of <see cref="ISessionSkillHotAttachWriter"/>. Reuses the same
/// materializer as cold-spawn so the workspace gets the canonical skill files, native vendor
/// pointers, and companion executable bins atomically.
/// </summary>
internal sealed class SessionSkillHotAttachWriter(
    SessionSkillPackageRegistry packages,
    ISessionSkillMaterializer skillMaterializer,
    IWorkspaceRootProvider workspaceRoot) : ISessionSkillHotAttachWriter
{
    public async Task<HotAttachMaterialization> MaterializeAsync(
        SessionSkillPackageResolution resolution,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        var resolved = packages.Resolve(resolution);
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", resolution.IntentId);

        await skillMaterializer.MaterializeAsync(
            workspacePath, resolution.Vendor, resolved, ct);

        return new HotAttachMaterialization(workspacePath);
    }
}
