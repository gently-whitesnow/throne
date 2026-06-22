using Throne.Application.Terminals;

namespace Throne.Application.Ports;

/// <summary>
/// Materializes hot-attached session-skill packages into the workspace and exposes the raw
/// SKILL.md text so the attach handler can compose a <c>&lt;system-reminder&gt;</c> for the live
/// tmux pane. Lives behind a port because Application must not touch the static skill source
/// root or the workspace <c>.claude/skills/</c> layout directly — Infrastructure owns those.
/// </summary>
public interface ISessionSkillHotAttachWriter
{
    /// <summary>
    /// Resolves the supplied session-skill ids to packages, writes
    /// <c>.claude/skills/{id}/SKILL.md</c> into the intent's workspace (so future spawns pick the
    /// skill up natively), and returns the SKILL.md content for each package in input order so the
    /// handler can quote it into the live-pane reminder.
    /// </summary>
    Task<HotAttachMaterialization> MaterializeAsync(
        SessionSkillPackageResolution resolution,
        CancellationToken ct);
}

public sealed record HotAttachedSkillContent(string SkillId, string SkillMarkdown);

public sealed record HotAttachMaterialization(
    string WorkspacePath,
    IReadOnlyList<HotAttachedSkillContent> Contents);
