using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/git-providers/github/repositories/{owner}/{repo}/pulls</c>.
/// Resolves the GitHub provider through <see cref="IGitProviderRegistry"/> and
/// delegates to <see cref="IGitProvider.ListPullRequestsAsync"/>. Only open
/// pull requests are returned for the bind-repository modal typeahead.
/// </summary>
public sealed class ListGithubRepositoryPullRequestsEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 10;

    public async Task<ActionResult<ICollection<GitPullRequestRefDto>>> RunAsync(
        string owner,
        string repo,
        string? q,
        int? limit,
        CancellationToken ct)
    {
        try
        {
            var provider = providers.GetByName(GitProviderNames.GitHub)
                ?? throw new ApiException(
                    ErrorCodes.RepositoryProviderUnsupported,
                    $"Git provider '{GitProviderNames.GitHub}' is not supported on this Throne build.");

            var refs = await provider.ListPullRequestsAsync(owner, repo, q, limit ?? DefaultLimit, ct);
            return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToPullRequestRefDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapListPullRequests(ex);
        }
    }
}
