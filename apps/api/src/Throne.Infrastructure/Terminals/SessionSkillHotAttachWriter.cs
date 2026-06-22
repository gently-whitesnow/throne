using System.Text;
using Throne.Application.Ports;
using Throne.Application.Terminals;

namespace Throne.Infrastructure.Terminals;

/// <summary>
/// Infrastructure implementation of <see cref="ISessionSkillHotAttachWriter"/>. Reuses
/// <see cref="SessionSkillWorkspaceFiles"/> for the workspace write (so future spawns pick the
/// skill up natively from <c>.claude/skills/{id}/SKILL.md</c>) and reads the same source-root
/// resolution for the live-pane reminder text — no duplication of skill-tree discovery.
/// </summary>
internal sealed class SessionSkillHotAttachWriter : ISessionSkillHotAttachWriter
{
    public async Task<IReadOnlyList<HotAttachedSkillContent>> WriteAsync(
        string workspacePath,
        IReadOnlyList<SessionSkillPackage> packages,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspacePath);
        ArgumentNullException.ThrowIfNull(packages);

        // Materialize into .claude/skills/{id}/SKILL.md so the live Claude session picks them up
        // natively on subsequent restarts (and right now, Claude reads SKILL.md from disk lazily).
        await SessionSkillWorkspaceFiles.WriteClaudeSkillsAsync(workspacePath, packages, ct);

        var contents = new List<HotAttachedSkillContent>(packages.Count);
        foreach (var package in packages)
        {
            var target = Path.Combine(workspacePath, ".claude", "skills", package.Id, "SKILL.md");
            var text = await File.ReadAllTextAsync(target, Encoding.UTF8, ct);
            contents.Add(new HotAttachedSkillContent(package.Id, text));
        }
        return contents;
    }
}
