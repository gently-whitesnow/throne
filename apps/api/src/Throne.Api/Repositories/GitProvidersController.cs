using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

public sealed class GitProvidersController(
    SearchGitProviderRepositoriesEndpoint searchEndpoint,
    ListGitProviderRepositoriesEndpoint listEndpoint,
    ListGitProviderRepositoryBranchesEndpoint branchesEndpoint,
    ListGitProviderRepositoryPullRequestsEndpoint pullsEndpoint) : GitProvidersControllerBase
{
    public override Task<ActionResult<ICollection<GitRepositoryRefDto>>> SearchGitProviderRepositories(
        string provider,
        string q = null!,
        RepositorySearchScope? scope = null,
        int? limit = null) =>
        searchEndpoint.RunAsync(provider, q, scope, limit, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<GitRepositoryRefDto>>> ListGitProviderRepositories(
        string provider,
        int? limit = null) =>
        listEndpoint.RunAsync(provider, limit, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<GitBranchRefDto>>> ListGitProviderRepositoryBranches(
        string provider,
        string owner,
        string repo,
        string q = null!,
        int? limit = null) =>
        branchesEndpoint.RunAsync(provider, owner, repo, q, limit, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<GitPullRequestRefDto>>> ListGitProviderRepositoryPullRequests(
        string provider,
        string owner,
        string repo,
        string q = null!,
        int? limit = null) =>
        pullsEndpoint.RunAsync(provider, owner, repo, q, limit, HttpContext.RequestAborted);
}
