namespace Throne.Application.Git;

/// <summary>
/// Aggregate on-disk size of a workspace directory. Implemented in Infrastructure — the
/// recursive filesystem walk must not leak into Application/Domain. Backs the workspace
/// "clean" summary (how many bytes a sweep frees / would free).
/// </summary>
public interface IWorkspaceDirectorySizer
{
    /// <summary>
    /// Total size in bytes of all files under <paramref name="absolutePath"/>. A missing
    /// directory returns 0; unreadable subtrees are skipped rather than throwing, so the
    /// figure is a best-effort lower bound on a partially-locked tree.
    /// </summary>
    long Measure(string absolutePath);
}
