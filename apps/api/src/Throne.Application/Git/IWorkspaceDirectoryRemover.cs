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
}
