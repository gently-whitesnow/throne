using Microsoft.AspNetCore.Mvc;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Bundles the two manual «sync» actions of a repository binding behind one injected
/// dependency so <see cref="IntentRepositoriesController"/> stays within its constructor
/// budget (same aggregation as <see cref="ReviewWorkspaceReadEndpoints"/>). Pure dispatch —
/// each method delegates to the dedicated endpoint:
///  - <see cref="SyncIntentRepositoryPullRequestEndpoint"/> — «Обновить» PR-метаданные;
///  - <see cref="SyncIntentRepositoryBranchEndpoint"/> — «Синхронизировать ветку» (git reset).
/// </summary>
public sealed class RepositorySyncEndpoints(
    SyncIntentRepositoryPullRequestEndpoint pullRequest,
    SyncIntentRepositoryBranchEndpoint branch)
{
    public Task<ActionResult<PullRequestSyncResultDto>> SyncPullRequestAsync(
        string intentId, string bindingId, CancellationToken ct) =>
        pullRequest.RunAsync(intentId, bindingId, ct);

    public Task<ActionResult<RepositoryBindingDto>> SyncBranchAsync(
        string intentId, string bindingId, CancellationToken ct) =>
        branch.RunAsync(intentId, bindingId, ct);
}
