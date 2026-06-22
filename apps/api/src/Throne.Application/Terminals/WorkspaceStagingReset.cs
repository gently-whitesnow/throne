namespace Throne.Application.Terminals;

/// <summary>
/// Wipes Throne-managed per-run staging from the workspace at the start of every spawn, before the
/// session is re-seeded (skills via the adapter, attachments via <see cref="WorkspaceAttachmentDumper"/>).
/// Fixed-name files (settings, system prompt) are overwritten each spawn and never accumulate.
/// Per-skill projections and scripts are written only when selected, so stale copies for every
/// managed skill id must be removed before each spawn. Only Throne-owned paths are touched; the
/// repo clone and operator-authored files are left intact.
/// </summary>
public static class WorkspaceStagingReset
{
    public static void Reset(string workspacePath, IEnumerable<string> managedSkillIds)
    {
        ArgumentNullException.ThrowIfNull(managedSkillIds);

        DeleteDirectory(WorkspaceAttachmentPaths.DirectoryPath(workspacePath));

        var skillIds = managedSkillIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        ResetVendorSkills(Path.Combine(workspacePath, ".claude", "skills"), skillIds);
        ResetVendorSkills(Path.Combine(workspacePath, ".agents", "skills"), skillIds);

        foreach (var skillId in skillIds)
        {
            DeleteDirectory(Path.Combine(workspacePath, "skills", skillId));
            DeleteFile(Path.Combine(workspacePath, $"throne-session.{skillId}.md"));
        }
    }

    private static void ResetVendorSkills(string skillsRoot, IReadOnlyList<string> managedSkillIds)
    {
        if (!Directory.Exists(skillsRoot))
        {
            return;
        }

        foreach (var skillDir in Directory.EnumerateDirectories(skillsRoot, "throne-*"))
        {
            DeleteDirectory(skillDir);
        }
        foreach (var skillId in managedSkillIds)
        {
            DeleteDirectory(Path.Combine(skillsRoot, skillId));
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
