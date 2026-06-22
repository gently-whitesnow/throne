using Microsoft.AspNetCore.Mvc;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

public sealed class GetGitProvidersStatusEndpoint(IGitProviderRegistry providers)
{
    public async Task<ActionResult<GitProvidersStatusDto>> RunAsync(CancellationToken ct)
    {
        // Probe every registered provider once, keyed by name — adding a provider only needs its
        // DI registration, the loop picks it up. Projecting the map onto the fixed wire fields
        // below stays a per-field mapper: the DTO is a closed OpenAPI object, so until the wire
        // schema opens (out of scope, ADR-0045 § «known tax») each field is named explicitly.
        var statuses = new Dictionary<string, GitProviderAuthStatusDto>(StringComparer.Ordinal);
        foreach (var provider in providers.AllProviders)
        {
            statuses[provider.ProviderName] = ToDto(await provider.GetAuthStatusAsync(ct));
        }

        var dto = new GitProvidersStatusDto
        {
            Github = statuses.GetValueOrDefault(GitProviderNames.GitHub) ?? Unknown(GitProviderNames.GitHub),
            Gitlab = statuses.GetValueOrDefault(GitProviderNames.GitLab) ?? Unknown(GitProviderNames.GitLab),
        };
        return new OkObjectResult(dto);
    }

    private static GitProviderAuthStatusDto Unknown(string provider) => new()
    {
        Authenticated = false,
        State = GitProviderAuthState.Missing,
        Error = $"Git provider '{provider}' is not configured on this Throne build.",
    };

    private static GitProviderAuthStatusDto ToDto(ProviderAuthStatus status)
    {
        var dto = new GitProviderAuthStatusDto
        {
            Authenticated = status.IsAuthenticated,
            State = ToWireState(status.State, status.IsAuthenticated),
            Host = status.Host,
            Login = status.Account!,
            Error = status.IsAuthenticated ? null! : status.Detail!,
        };
        if (status.Scopes.Count > 0)
        {
            dto.Scopes = status.Scopes.ToList();
        }
        return dto;
    }

    private static GitProviderAuthState ToWireState(string state, bool isAuthenticated) => state switch
    {
        ProviderAuthStateNames.Authenticated => GitProviderAuthState.Authenticated,
        ProviderAuthStateNames.Offline => GitProviderAuthState.Offline,
        ProviderAuthStateNames.Missing => GitProviderAuthState.Missing,
        ProviderAuthStateNames.Unauthenticated => GitProviderAuthState.Unauthenticated,
        _ => isAuthenticated ? GitProviderAuthState.Authenticated : GitProviderAuthState.Unauthenticated,
    };
}
