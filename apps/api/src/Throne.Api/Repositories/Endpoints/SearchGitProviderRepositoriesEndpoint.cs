using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Git;
using Throne.Repositories.Contracts.Generated;
using AppScope = Throne.Application.Git.RepositorySearchScope;
using WireScope = Throne.Repositories.Contracts.Generated.RepositorySearchScope;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// 422 surfaces both <c>provider_unsupported</c> and <c>provider_not_authenticated</c>
/// per ADR-0024 § 3.
/// </summary>
public sealed class SearchGitProviderRepositoriesEndpoint(IGitProviderRegistry providers)
{
    private const int DefaultLimit = 30;

    public async Task<ActionResult<ICollection<GitRepositoryRefDto>>> RunAsync(
        GitProvider provider,
        string? q,
        WireScope? scope,
        int? limit,
        CancellationToken ct)
    {
        try
        {
            var providerName = RepositoryEnumDtoMapper.ToProviderName(provider);
            var gitProvider = providers.GetByName(providerName)
                ?? throw Unsupported(providerName);

            var resolvedScope = scope is null ? AppScope.Mine : ToApplicationScope(scope.Value);
            var refs = await gitProvider.SearchRepositoriesAsync(resolvedScope, q, limit ?? DefaultLimit, ct);
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

    private static ApiException Unsupported(string providerName) =>
        new(
            ErrorCodes.RepositoryProviderUnsupported,
            $"Git provider '{providerName}' is not supported on this Throne build.");
}
