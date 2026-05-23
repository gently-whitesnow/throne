using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;
using AppScope = Throne.Application.Git.RepositorySearchScope;
using WireScope = Throne.Repositories.Contracts.Generated.RepositorySearchScope;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/git-providers/github/repositories/search</c> (T-11).
/// Resolves the GitHub provider through <see cref="IGitProviderRegistry"/> and
/// delegates the search to <see cref="IGitProvider.SearchRepositoriesAsync"/>.
/// 422 surfaces both <c>provider_unsupported</c> and <c>provider_not_authenticated</c>
/// per ADR-0024 § 3.
/// </summary>
public sealed class SearchGithubRepositoriesEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 30;

    public async Task<ActionResult<ICollection<GitRepositoryRefDto>>> RunAsync(
        string? q,
        WireScope? scope,
        int? limit,
        CancellationToken ct)
    {
        try
        {
            var provider = providers.GetByName(GitProviderNames.GitHub)
                ?? throw new ApiException(
                    ErrorCodes.RepositoryProviderUnsupported,
                    $"Git provider '{GitProviderNames.GitHub}' is not supported on this Throne build.");

            var resolvedScope = scope is null ? AppScope.Mine : ToApplicationScope(scope.Value);
            var refs = await provider.SearchRepositoriesAsync(resolvedScope, q, limit ?? DefaultLimit, ct);
            return new OkObjectResult(refs.Select(RepositoryDtoMapper.ToRepositoryRefDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapSearch(ex);
        }
    }

    private static AppScope ToApplicationScope(WireScope scope) => scope switch
    {
        WireScope.Mine => AppScope.Mine,
        WireScope.Involved => AppScope.Involved,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown search scope."),
    };
}
