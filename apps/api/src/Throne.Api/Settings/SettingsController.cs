using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Settings.Endpoints;
using Throne.Application.Git;
using Throne.Application.Terminals;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings;

public sealed class SettingsController(
    GetWorkspaceSettingsEndpoint workspaceEndpoint,
    CleanWorkspaceEndpoint cleanEndpoint,
    GetGitProvidersStatusEndpoint providersEndpoint,
    GetLocalModelCatalogEndpoint localModelEndpoint,
    TerminalSettingsService terminalSettings,
    SkillModeDefaultsService skillModeDefaults,
    IGitLabHostProvider gitLabHost,
    TaskTrackerConnectionsEndpoint taskTrackerConnections,
    TaskTrackerBoardsEndpoint taskTrackerBoards) : SettingsControllerBase
{
    public override Task<ActionResult<WorkspaceSettingsDto>> GetWorkspaceSettings() =>
        Task.FromResult(workspaceEndpoint.Run());

    public override Task<ActionResult<WorkspaceCleanResultDto>> CleanWorkspace(WorkspaceCleanRequestDto body) =>
        cleanEndpoint.RunAsync(body, HttpContext.RequestAborted);

    public override Task<ActionResult<GitProvidersStatusDto>> GetGitProvidersStatus() =>
        providersEndpoint.RunAsync(HttpContext.RequestAborted);

    public override Task<ActionResult<LocalModelCatalogDto>> GetLocalModelCatalog() =>
        localModelEndpoint.RunAsync(HttpContext.RequestAborted);

    public override async Task<ActionResult<TerminalSettingsDto>> GetTerminalSettings()
    {
        var vendor = await terminalSettings.GetDefaultVendorAsync(HttpContext.RequestAborted);
        return Ok(new TerminalSettingsDto { Default_vendor = vendor });
    }

    public override async Task<ActionResult<TerminalSettingsDto>> SetTerminalSettings(UpdateTerminalSettingsRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var saved = await terminalSettings.SetDefaultVendorAsync(
            body.Default_vendor, HttpContext.RequestAborted);
        return Ok(new TerminalSettingsDto { Default_vendor = saved });
    }

    public override async Task<ActionResult<SkillModeDefaultsDto>> GetSkillModeDefaults()
    {
        var view = await skillModeDefaults.GetAsync(HttpContext.RequestAborted);
        return Ok(SkillModeDefaultsDtoMapper.ToDto(view));
    }

    public override async Task<ActionResult<SkillModeDefaultsDto>> SetSkillModeDefaults(
        UpdateSkillModeDefaultsRequest body)
    {
        var view = await skillModeDefaults.ReplaceAsync(
            SkillModeDefaultsDtoMapper.ToDomain(body),
            HttpContext.RequestAborted);
        return Ok(SkillModeDefaultsDtoMapper.ToDto(view));
    }

    public override async Task<ActionResult<GitLabHostSettingsDto>> SetGitLabHost(UpdateGitLabHostRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var host = await gitLabHost.SetHostAsync(body.Host, HttpContext.RequestAborted);
        return Ok(new GitLabHostSettingsDto { Host = host });
    }

    public override Task<ActionResult<TaskTrackerConnectionsDto>> GetTaskTrackerConnections() =>
        taskTrackerConnections.ListAsync(HttpContext.RequestAborted);

    public override Task<ActionResult<TaskTrackerConnectionDto>> SetTaskTrackerConnection(
        string tracker, UpdateTaskTrackerConnectionRequest body) =>
        taskTrackerConnections.SetAsync(tracker, body, HttpContext.RequestAborted);

    public override Task<IActionResult> DeleteTaskTrackerConnection(string tracker) =>
        taskTrackerConnections.DeleteAsync(tracker, HttpContext.RequestAborted);

    public override Task<ActionResult<TaskTrackerBoardSearchDto>> SearchTaskTrackerBoards(
        string tracker, string query = null!, int? skip = 0, int? take = 20, bool? refresh = false) =>
        taskTrackerBoards.SearchAsync(
            tracker, query, skip ?? 0, take ?? 20, refresh ?? false, HttpContext.RequestAborted);

    public override Task<ActionResult<TaskTrackerBoardSelectionDto>> GetTaskTrackerBoardSelection(string tracker) =>
        taskTrackerBoards.GetSelectionAsync(tracker, HttpContext.RequestAborted);

    public override Task<ActionResult<TaskTrackerBoardSelectionDto>> SetTaskTrackerBoards(
        string tracker, UpdateTaskTrackerBoardsRequest body) =>
        taskTrackerBoards.SetAsync(tracker, body, HttpContext.RequestAborted);

}
