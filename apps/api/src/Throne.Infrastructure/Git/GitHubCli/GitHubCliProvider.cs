using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// <see cref="IGitProvider"/> implementation backed by shell-out to the
/// <c>gh</c> CLI per ADR-0024 § 3. Slice 1 / T-06 ships repo-operations only:
/// search, list, clone, fetch and an auth-status probe. PR snapshot and review
/// comments (T-07) throw <see cref="NotSupportedException"/> until the PR
/// surface lands — this keeps DI wiring stable for downstream consumers (T-08,
/// T-09, T-11) without forcing them to handle two implementations.
/// All real work lives in the four collaborator types (<see cref="GhRepoSearcher"/>,
/// <see cref="GhRepoActions"/>, <see cref="GhAuthProbe"/>, <see cref="GhCliInvoker"/>),
/// keeping this façade trivial and inside the CA1502 budget.
/// </summary>
internal sealed class GitHubCliProvider(
    GhRepoSearcher searcher,
    GhRepoActions actions,
    GhAuthProbe authProbe) : IGitProvider
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

    public Task CloneRepositoryAsync(string owner, string repo, string targetPath, CancellationToken ct) =>
        actions.CloneAsync(owner, repo, targetPath, ct);

    public Task FetchRepositoryAsync(string workspacePath, CancellationToken ct) =>
        actions.FetchAsync(workspacePath, ct);

    public Task<ProviderAuthStatus> GetAuthStatusAsync(CancellationToken ct) =>
        authProbe.ProbeAsync(ct);

    // ---- PR surface — implemented in T-07. -------------------------------------------------

    public Task<PullRequestSnapshot?> GetPullRequestAsync(string owner, string repo, int number, CancellationToken ct) =>
        throw new NotSupportedException("Pull-request operations land in T-07.");

    public Task<PullRequestCommentsPage?> ListPullRequestCommentsAsync(
        string owner, string repo, int number, string? etag, CancellationToken ct) =>
        throw new NotSupportedException("Pull-request operations land in T-07.");
}
