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
        string provider,
        int? limit,
        CancellationToken ct
    )
    {
        var gitProvider = providers.GetByName(provider) ?? throw Unsupported(provider);

        var refs = await gitProvider.ListUserRepositoriesAsync(limit ?? DefaultLimit, ct);
        return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToRepositoryRefDto).ToList());
    }

    private static ApiException Unsupported(string providerName) =>
        new(
            ErrorCodes.RepositoryProviderUnsupported,
            $"Git provider '{providerName}' is not supported on this Throne build."
        );
}
