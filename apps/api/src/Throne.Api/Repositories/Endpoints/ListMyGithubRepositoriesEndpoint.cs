using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Default-state alias for the bind modal — same as search with
/// <c>scope=mine</c> and no query, but spared the parameter wiring.
/// </summary>
public sealed class ListMyGithubRepositoriesEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 30;

    public async Task<ActionResult<ICollection<GitRepositoryRefDto>>> RunAsync(int? limit, CancellationToken ct)
    {
        try
        {
            var provider = providers.GetByName(GitProviderNames.GitHub)
                ?? throw new ApiException(
                    ErrorCodes.RepositoryProviderUnsupported,
                    $"Git provider '{GitProviderNames.GitHub}' is not supported on this Throne build.");

            var refs = await provider.ListUserRepositoriesAsync(limit ?? DefaultLimit, ct);
            return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToRepositoryRefDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapSearch(ex);
        }
    }
}
