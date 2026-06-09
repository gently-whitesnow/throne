namespace Throne.Application.Git;

/// <summary>
/// Existence check for a binding's on-disk workspace directory. Implemented in
/// Infrastructure — the filesystem probe must not leak into Application/Domain.
/// Backs the «Обновить» disk-recovery path (ADR-0024): a binding may exist in Mongo
/// while its local clone folder is absent on the current machine.
/// </summary>
public interface IWorkspaceDirectoryProbe
{
    /// <summary>
    /// True when <paramref name="absolutePath"/> is an existing directory. Pure
    /// existence — no `.git` integrity / remote check (that case is out of scope).
    /// </summary>
    bool Exists(string absolutePath);
}
