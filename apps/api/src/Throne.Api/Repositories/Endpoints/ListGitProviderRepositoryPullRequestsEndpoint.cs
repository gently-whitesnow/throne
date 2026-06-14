using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/git-providers/{provider}/repositories/{owner}/{repo}/pulls</c>.
/// Resolves the provider through <see cref="IGitProviderRegistry"/> and
/// delegates to <see cref="IGitProvider.ListPullRequestsAsync"/>. Only open
/// pull requests are returned for the bind-repository modal typeahead.
/// </summary>
public sealed class ListGitProviderRepositoryPullRequestsEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 10;

    public async Task<ActionResult<ICollection<GitPullRequestRefDto>>> RunAsync(
        GitProvider provider,
        string owner,
        string repo,
        string? q,
        int? limit,
        CancellationToken ct
    )
    {
        var providerName = RepositoryEnumDtoMapper.ToProviderName(provider);
        var gitProvider = providers.GetByName(providerName) ?? throw Unsupported(providerName);

        var refs = await gitProvider.ListPullRequestsAsync(
            owner,
            repo,
            q,
            limit ?? DefaultLimit,
            ct
        );
        return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToPullRequestRefDto).ToList());
    }

    private static ApiException Unsupported(string providerName) =>
        new(
            ErrorCodes.RepositoryProviderUnsupported,
            $"Git provider '{providerName}' is not supported on this Throne build."
        );
}
