using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GitLabCliProvider(
    GlabRepoSearcher searcher,
    GlabRepoActions actions,
    GlabAuthProbe authProbe,
    GlabPullRequestActions pullRequests,
    GlabRefListers refListers) : IGitProvider
{
    public string ProviderName => GitProviderNames.GitLab;

    public Task<ProviderAuthStatus> GetAuthStatusAsync(CancellationToken ct) =>
        authProbe.ProbeAsync(ct);

    public Task<IReadOnlyList<GitRepositoryRef>> SearchRepositoriesAsync(
        RepositorySearchScope scope,
        string? query,
        int limit,
        CancellationToken ct) =>
        searcher.SearchAsync(scope, query, limit, ct);

    public Task<IReadOnlyList<GitRepositoryRef>> ListUserRepositoriesAsync(int limit, CancellationToken ct) =>
        searcher.SearchAsync(RepositorySearchScope.Mine, query: null, limit, ct);

    public Task CloneRepositoryAsync(string owner, string repo, string targetPath, CancellationToken ct) =>
        actions.CloneAsync(owner, repo, targetPath, ct);

    public Task SyncRepositoryAsync(string workspacePath, CancellationToken ct) =>
        actions.SyncAsync(workspacePath, ct);

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

    public Task<PullRequestSnapshot?> GetPullRequestAsync(string owner, string repo, int number, CancellationToken ct) =>
        pullRequests.GetAsync(owner, repo, number, ct);

    public Task<PullRequestCommentsPage?> ListPullRequestCommentsAsync(
        string owner,
        string repo,
        int number,
        string? etag,
        CancellationToken ct) =>
        pullRequests.ListCommentsAsync(owner, repo, number, ct);
}
