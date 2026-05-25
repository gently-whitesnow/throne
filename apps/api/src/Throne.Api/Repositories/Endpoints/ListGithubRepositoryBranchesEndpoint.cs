using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/git-providers/github/repositories/{owner}/{repo}/branches</c>.
/// Resolves the GitHub provider through <see cref="IGitProviderRegistry"/> and
/// delegates to <see cref="IGitProvider.ListBranchesAsync"/>. Mirrors
/// <see cref="SearchGithubRepositoriesEndpoint"/> for the branch typeahead in
/// the bind-repository modal.
/// </summary>
public sealed class ListGithubRepositoryBranchesEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 10;

    public async Task<ActionResult<ICollection<GitBranchRefDto>>> RunAsync(
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

            var refs = await provider.ListBranchesAsync(owner, repo, q, limit ?? DefaultLimit, ct);
            return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToBranchRefDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapListBranches(ex);
        }
    }
}
