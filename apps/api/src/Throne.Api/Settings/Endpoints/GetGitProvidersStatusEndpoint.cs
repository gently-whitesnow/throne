using Microsoft.AspNetCore.Mvc;
using Throne.Application.Git;
using Throne.Domain.Repositories;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/settings/git-providers/status</c> (T-11, see T-06).
/// Slice 1 reports only the GitHub provider; the response is shaped so T-16 / T-17
/// (settings page) can render a row per provider without hard-coding the list.
/// </summary>
public sealed class GetGitProvidersStatusEndpoint(IGitProviderRegistry providers)
{
    public async Task<ActionResult<GitProvidersStatusDto>> RunAsync(CancellationToken ct)
    {
        var github = providers.GetByName(GitProviderNames.GitHub);
        var dto = new GitProvidersStatusDto
        {
            Github = github is null
                ? Unknown()
                : ToDto(await github.GetAuthStatusAsync(ct)),
        };
        return new OkObjectResult(dto);
    }

    private static GitProviderAuthStatusDto Unknown() => new()
    {
        Authenticated = false,
        Error = "Git provider 'github' is not configured on this Throne build.",
    };

    private static GitProviderAuthStatusDto ToDto(ProviderAuthStatus status)
    {
        var dto = new GitProviderAuthStatusDto
        {
            Authenticated = status.IsAuthenticated,
            Login = status.Account!,
            Error = status.IsAuthenticated ? null! : status.Detail!,
        };
        if (status.Scopes.Count > 0)
        {
            dto.Scopes = status.Scopes.ToList();
        }
        return dto;
    }
}
