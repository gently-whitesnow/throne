using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Settings.Endpoints;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings;

public sealed class SettingsController(
    GetWorkspaceSettingsEndpoint workspaceEndpoint,
    GetGitProvidersStatusEndpoint providersEndpoint) : SettingsControllerBase
{
    public override Task<ActionResult<WorkspaceSettingsDto>> GetWorkspaceSettings() =>
        Task.FromResult(workspaceEndpoint.Run());

    public override Task<ActionResult<GitProvidersStatusDto>> GetGitProvidersStatus() =>
        providersEndpoint.RunAsync(HttpContext.RequestAborted);
}
