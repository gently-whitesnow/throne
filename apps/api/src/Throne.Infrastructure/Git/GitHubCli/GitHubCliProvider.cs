using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// <see cref="IGitProvider"/> implementation backed by shell-out to the
/// <c>gh</c> CLI per ADR-0024 § 3. Slice 1 covers repo-operations (T-06:
/// search, list, clone, fetch, auth probe) and the pull-request surface
/// (T-07: PR snapshot + review-comments feed with conditional GET via ETag).
/// All real work lives in the five collaborator types (<see cref="GhRepoSearcher"/>,
/// <see cref="GhRepoActions"/>, <see cref="GhAuthProbe"/>,
/// <see cref="GhPullRequestActions"/>, <see cref="GhCliInvoker"/>), keeping
/// this façade trivial and inside the CA1502 budget.
/// </summary>
internal sealed class GitHubCliProvider(
    GhRepoSearcher searcher,
    GhRepoActions actions,
    GhAuthProbe authProbe,
    GhPullRequestActions pullRequests,
    GhRefListers refListers) : IGitProvider
{
    public string ProviderName => GitProviderNames.GitHub;

    public Task<IReadOnlyList<GitRepositoryRef>> ListUserRepositoriesAsync(int limit, CancellationToken ct) =>
        searcher.SearchAsync(RepositorySearchScope.Mine, query: null, limit, ct);

    public Task<IReadOnlyList<GitRepositoryRef>> SearchRepositoriesAsync(
        RepositorySearchScope scope,
        string? query,
        int limit,
        CancellationToken ct) =>
        searcher.SearchAsync(scope, query, limit, ct);

    public Task<IReadOnlyList<GitBranchRef>> ListBranchesAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct) =>
        refListers.Branches.ListAsync(owner, repo, query, limit, ct);

    public Task<IReadOnlyList<GitPullRequestRef>> ListPullRequestsAsync(
        string owner,
        string repo,
        string? query,
        int limit,
        CancellationToken ct) =>
        refListers.PullRequests.ListAsync(owner, repo, query, limit, ct);

    public Task CloneRepositoryAsync(string owner, string repo, string targetPath, CancellationToken ct) =>
        actions.CloneAsync(owner, repo, targetPath, ct);

    public Task SyncRepositoryAsync(string workspacePath, CancellationToken ct) =>
        actions.SyncAsync(workspacePath, ct);

    public Task<ProviderAuthStatus> GetAuthStatusAsync(CancellationToken ct) =>
        authProbe.ProbeAsync(ct);

    public Task<PullRequestSnapshot?> GetPullRequestAsync(string owner, string repo, int number, CancellationToken ct) =>
        pullRequests.GetAsync(owner, repo, number, ct);

    public Task<PullRequestCommentsPage?> ListPullRequestCommentsAsync(
        string owner, string repo, int number, string? etag, CancellationToken ct) =>
        pullRequests.ListReviewCommentsAsync(owner, repo, number, etag, ct);
}
