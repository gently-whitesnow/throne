using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Default-state alias for the bind modal — same as search with
/// <c>scope=mine</c> and no query, but spared the parameter wiring.
/// </summary>
public sealed class ListGitProviderRepositoriesEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 30;

    public async Task<ActionResult<ICollection<GitRepositoryRefDto>>> RunAsync(
        GitProvider provider,
        int? limit,
        CancellationToken ct)
    {
        try
        {
            var providerName = RepositoryEnumDtoMapper.ToProviderName(provider);
            var gitProvider = providers.GetByName(providerName)
                ?? throw Unsupported(providerName);

            var refs = await gitProvider.ListUserRepositoriesAsync(limit ?? DefaultLimit, ct);
            return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToRepositoryRefDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapSearch(ex);
        }
    }

    private static ApiException Unsupported(string providerName) =>
        new(
            ErrorCodes.RepositoryProviderUnsupported,
            $"Git provider '{providerName}' is not supported on this Throne build.");
}
