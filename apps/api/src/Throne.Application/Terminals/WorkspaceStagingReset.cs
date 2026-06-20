namespace Throne.Application.Terminals;

/// <summary>
/// Wipes Throne-managed per-run staging from the workspace at the start of every spawn, before the
/// session is re-seeded (skills via the adapter, attachments via <see cref="WorkspaceAttachmentDumper"/>).
/// Fixed-name files (settings, system prompt) are overwritten each spawn and never accumulate, but
/// staged attachments and the <c>.claude/skills/throne-*</c> trees are written per name and would
/// otherwise leak across runs — e.g. a <c>throne-review-artifact</c> skill written in a review run
/// would linger after the next run switches to <c>work</c>. Only Throne-owned paths are touched; the
/// repo clone and operator-authored files are left intact.
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
        }
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
