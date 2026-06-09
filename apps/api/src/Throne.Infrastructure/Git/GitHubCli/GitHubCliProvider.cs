using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// <see cref="IGitProvider"/> implementation backed by shell-out to the
/// <c>gh</c> CLI per ADR-0024 § 3. Covers repo-operations (search, list, clone,
/// fetch, auth probe) and the pull-request surface (PR snapshot + review-comments
/// feed with conditional GET via ETag). All real work lives in the collaborator
/// types so this façade stays trivial.
/// </summary>
internal sealed class GitHubCliProvider(
    GhRepoSearcher searcher,
    GhRepoActions actions,
    GhAuthProbe authProbe,
    GhPullRequestActions pullRequests,
    GhRefListers refListers,
    GhReviewWorkspaceActions reviewWorkspace) : IGitProvider
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

    public Task<PullRequestDiff?> GetPullRequestDiffAsync(string owner, string repo, int number, CancellationToken ct) =>
        reviewWorkspace.GetPullRequestDiffAsync(owner, repo, number, ct);

    public Task<PullRequestDiff?> GetCommitDiffAsync(string owner, string repo, string commitSha, CancellationToken ct) =>
        reviewWorkspace.GetCommitDiffAsync(owner, repo, commitSha, ct);

    public Task<IReadOnlyList<PullRequestCommitRef>?> ListPullRequestCommitsAsync(string owner, string repo, int number, CancellationToken ct) =>
        reviewWorkspace.ListCommitsAsync(owner, repo, number, ct);

    public Task<SubmittedReviewComment> SubmitReviewCommentAsync(
        string owner, string repo, int number, SubmitReviewCommentRequest request, CancellationToken ct) =>
        reviewWorkspace.SubmitReviewCommentAsync(owner, repo, number, request, ct);

    public Task<ReviewThreadState> ResolveReviewThreadAsync(
        string owner, string repo, int number, string threadId, bool resolved, CancellationToken ct) =>
        reviewWorkspace.ResolveReviewThreadAsync(owner, repo, number, threadId, resolved, ct);

    public Task DeleteReviewCommentAsync(
        string owner, string repo, int number, string commentId, string? threadId, CancellationToken ct) =>
        reviewWorkspace.DeleteReviewCommentAsync(owner, repo, number, commentId, threadId, ct);
}
