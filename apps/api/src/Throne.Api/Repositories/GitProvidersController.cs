using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Repositories.Endpoints;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

public sealed class GitProvidersController(
    SearchGithubRepositoriesEndpoint searchEndpoint,
    ListMyGithubRepositoriesEndpoint listMyEndpoint,
    ListGithubRepositoryBranchesEndpoint branchesEndpoint,
    ListGithubRepositoryPullRequestsEndpoint pullsEndpoint) : GitProvidersControllerBase
{
    public override Task<ActionResult<ICollection<GitRepositoryRefDto>>> SearchGithubRepositories(
        string q = null!,
        RepositorySearchScope? scope = null,
        int? limit = null) =>
        searchEndpoint.RunAsync(q, scope, limit, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<GitRepositoryRefDto>>> ListMyGithubRepositories(
        int? limit = null) =>
        listMyEndpoint.RunAsync(limit, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<GitBranchRefDto>>> ListGithubRepositoryBranches(
        string owner,
        string repo,
        string q = null!,
        int? limit = null) =>
        branchesEndpoint.RunAsync(owner, repo, q, limit, HttpContext.RequestAborted);

    public override Task<ActionResult<ICollection<GitPullRequestRefDto>>> ListGithubRepositoryPullRequests(
        string owner,
        string repo,
        string q = null!,
        int? limit = null) =>
        pullsEndpoint.RunAsync(owner, repo, q, limit, HttpContext.RequestAborted);
}
