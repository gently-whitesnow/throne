namespace Throne.Application.Terminals;

/// <summary>
/// Wipes Throne-managed per-run staging from the workspace at the start of every spawn, before the
/// session is re-seeded (skills via the adapter, attachments via <see cref="WorkspaceAttachmentDumper"/>).
/// Fixed-name files (settings, system prompt) are overwritten each spawn and never accumulate.
/// Per-skill projections and scripts are written only when selected, so stale copies must be
/// removed before each spawn. Only Throne-owned paths are touched; the repo clone and
/// operator-authored files are left intact.
/// </summary>
public static class WorkspaceStagingReset
{
    public static void Reset(string workspacePath)
    {
        DeleteDirectory(WorkspaceAttachmentPaths.DirectoryPath(workspacePath));

        var skillsRoot = Path.Combine(workspacePath, ".claude", "skills");
        if (Directory.Exists(skillsRoot))
        {
            foreach (var skillDir in Directory.EnumerateDirectories(skillsRoot, "throne-*"))
            {
                DeleteDirectory(skillDir);
            }
            DeleteDirectory(Path.Combine(skillsRoot, SessionSkillPackageIds.Intent));
            DeleteDirectory(Path.Combine(skillsRoot, SessionSkillPackageIds.Review));
            DeleteDirectory(Path.Combine(skillsRoot, SessionSkillPackageIds.Dream));
        }

        DeleteFile(Path.Combine(workspacePath, "bin", "throne-intent"));
        DeleteFile(Path.Combine(workspacePath, "bin", "throne-review"));
        DeleteFile(Path.Combine(workspacePath, "bin", "throne-dream"));
        DeleteFile(Path.Combine(workspacePath, "throne-session.intent.md"));
        DeleteFile(Path.Combine(workspacePath, "throne-session.review.md"));
        DeleteFile(Path.Combine(workspacePath, "throne-session.dream.md"));
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
