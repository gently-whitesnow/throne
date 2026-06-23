using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// «Синхронизировать ветку»: hard-sync the local clone's current branch to its remote tip
/// via <see cref="ILocalGitWorkspaceSync"/> (<c>git fetch</c> + <c>git reset --hard
/// origin/{branch}</c>). Deliberately separate from «Обновить» (<c>RefreshAsync</c>): this is
/// the only action that rewrites the working tree, discarding uncommitted local changes.
/// Kept out of <see cref="RepositoryBindingService"/> so that service's dependency budget is
/// not stretched — the git seam is needed only here (same split as the other repository
/// use-cases). Requires a <c>ready</c> clone whose folder is present on disk; the binding
/// record is unchanged, so it is returned verbatim for the UI to re-render.
/// </summary>
public sealed class SyncRepositoryBranchUseCase(
    RepositoryBindingResolver resolver,
    RepositoryBindingPersistence persistence,
    ILocalGitWorkspaceSync workspaceSync)
{
    public async Task<IntentRepositoryBinding> ExecuteAsync(
        SyncRepositoryBranchCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);

        var binding = await resolver.LoadBindingAsync(command.IntentId, command.BindingId, ct);
        if (binding.State.CloneStatus != CloneStatusNames.Ready || !persistence.LocalCloneExists(binding))
        {
            throw RepositoryBindingFailures.BindingNotReady(binding);
        }

        try
        {
            await workspaceSync.SyncCurrentBranchToRemoteAsync(persistence.ResolveWorkspacePath(binding), ct);
        }
        catch (GitProviderException ex)
        {
            throw RepositoryBindingFailures.BranchSyncFailed(binding, ex);
        }

        return binding;
    }
}
