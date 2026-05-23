using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Settings.Endpoints;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings;

/// <summary>
/// HTTP surface for <c>/api/v1/settings/*</c> (T-11). Drives the workspace and
/// git-provider sections of the settings page (T-16, T-17). Slice 1 returns
/// only the workspace root + GitHub auth status.
/// </summary>
public sealed class SettingsController(
    GetWorkspaceSettingsEndpoint workspaceEndpoint,
    GetGitProvidersStatusEndpoint providersEndpoint) : SettingsControllerBase
{
    public override Task<ActionResult<WorkspaceSettingsDto>> GetWorkspaceSettings() =>
        Task.FromResult(workspaceEndpoint.Run());

    public override Task<ActionResult<GitProvidersStatusDto>> GetGitProvidersStatus() =>
        providersEndpoint.RunAsync(HttpContext.RequestAborted);
}
