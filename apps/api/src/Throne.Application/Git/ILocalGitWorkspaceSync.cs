namespace Throne.Application.Git;

/// <summary>
/// Hard-syncs the currently checked-out branch of a local clone to its remote tip
/// (<c>git fetch</c> + <c>git reset --hard origin/{branch}</c>). Provider-agnostic plain
/// git, kept out of <see cref="IGitProvider"/> (which is the vendor-API surface) — same seam
/// as <see cref="ILocalGitBranchReader"/>. Backs the «Синхронизировать ветку» action: unlike
/// «Обновить» (PR-metadata + disk-recovery), this is the only path that touches the working
/// tree. The reset is destructive on purpose — uncommitted changes are discarded so the
/// branch always lands exactly on the remote tip (operator is warned in the UI confirm).
/// </summary>
public interface ILocalGitWorkspaceSync
{
    /// <summary>
    /// Fetch <c>origin</c> and reset the current branch to <c>origin/{branch}</c>.
    /// Throws <see cref="GitProviderException"/> on any failure: detached HEAD, no
    /// upstream, fetch/reset non-zero exit, missing git binary.
    /// </summary>
    Task SyncCurrentBranchToRemoteAsync(string workspacePath, CancellationToken ct);
}
