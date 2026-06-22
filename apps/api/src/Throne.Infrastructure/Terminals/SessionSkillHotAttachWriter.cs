using System.Text;
using Throne.Application.Git;
using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Infrastructure implementation of <see cref="ISessionSkillHotAttachWriter"/>. Reuses
/// <see cref="SessionSkillWorkspaceFiles"/> for the workspace write (so future spawns pick the
/// skill up natively from <c>.claude/skills/{id}/SKILL.md</c>) and reads the same source-root
/// resolution for the live-pane reminder text — no duplication of skill-tree discovery.
/// </summary>
internal sealed class SessionSkillHotAttachWriter(
    SessionSkillPackageRegistry packages,
    IWorkspaceRootProvider workspaceRoot) : ISessionSkillHotAttachWriter
{
    public async Task<HotAttachMaterialization> MaterializeAsync(
        SessionSkillPackageResolution resolution,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(resolution);

        var resolved = packages.Resolve(resolution);
        var workspacePath = Path.Combine(workspaceRoot.ResolvedRoot, "intents", resolution.IntentId);

        await SessionSkillWorkspaceFiles.WriteClaudeSkillsAsync(workspacePath, resolved, ct);

        var contents = new List<HotAttachedSkillContent>(resolved.Count);
        foreach (var package in resolved)
        {
            var target = Path.Combine(workspacePath, ".claude", "skills", package.Id, "SKILL.md");
            var text = await File.ReadAllTextAsync(target, Encoding.UTF8, ct);
            contents.Add(new HotAttachedSkillContent(package.Id, text));
        }
        return new HotAttachMaterialization(workspacePath, contents);
    }
}
