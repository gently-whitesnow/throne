namespace Throne.Application.Git;

/// <summary>
/// Removes a binding's on-disk workspace directory when the repository is deleted
/// from an intent (ADR-0024 § 1, revised). Implemented in Infrastructure — the
/// recursive filesystem delete must not leak into Application/Domain.
/// </summary>
public interface IWorkspaceDirectoryRemover
{
    /// <summary>
    /// Recursively delete <paramref name="absolutePath"/>. A missing directory is a
    /// no-op so the caller stays idempotent. Throws on a real IO/permission failure
    /// so the caller can keep the binding and surface the error.
    /// </summary>
    Task RemoveAsync(string absolutePath, CancellationToken ct);

    /// <summary>
    /// Delete every entry directly under <paramref name="directoryPath"/> while keeping
    /// the directory itself. Backs the workspace "clean all" sweep: after every binding
    /// is unbound, this clears leftover empty intent folders and any orphan clones so the
    /// root ends up empty. A missing directory is a no-op; throws on a real IO failure.
    /// </summary>
    Task RemoveContentsAsync(string directoryPath, CancellationToken ct);
}
