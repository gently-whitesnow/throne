using Microsoft.AspNetCore.Mvc;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

public sealed class GetGitProvidersStatusEndpoint(IGitProviderRegistry providers)
{
    public async Task<ActionResult<GitProvidersStatusDto>> RunAsync(CancellationToken ct)
    {
        var github = providers.GetByName(GitProviderNames.GitHub);
        var gitlab = providers.GetByName(GitProviderNames.GitLab);
        var dto = new GitProvidersStatusDto
        {
            Github = github is null
                ? Unknown(GitProviderNames.GitHub)
                : ToDto(await github.GetAuthStatusAsync(ct)),
            Gitlab = gitlab is null
                ? Unknown(GitProviderNames.GitLab)
                : ToDto(await gitlab.GetAuthStatusAsync(ct)),
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
