using Throne.Application.Terminals;

namespace Throne.Application.Ports;

/// <summary>
/// Materializes hot-attached session-skill packages into the workspace. Lives behind a port
/// because Application must not touch the static skill source root or vendor-specific skill
/// layouts directly — Infrastructure owns those.
/// </summary>
public interface ISessionSkillHotAttachWriter
{
    /// <summary>
    /// Resolves the supplied session-skill ids to packages and writes the canonical
    /// <c>skills/{id}/SKILL.md</c> plus the active vendor's native skill projection.
    /// </summary>
    Task<HotAttachMaterialization> MaterializeAsync(
        SessionSkillPackageResolution resolution,
        CancellationToken ct);
}

public sealed record HotAttachMaterialization(string WorkspacePath);
