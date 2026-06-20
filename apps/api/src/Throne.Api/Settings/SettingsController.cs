using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Settings.Endpoints;
using Throne.Application.Terminals;
using Throne.Settings.Contracts.Generated;

namespace Throne.Api.Settings;

public sealed class SettingsController(
    GetWorkspaceSettingsEndpoint workspaceEndpoint,
    CleanWorkspaceEndpoint cleanEndpoint,
    GetGitProvidersStatusEndpoint providersEndpoint,
    GetLocalModelCatalogEndpoint localModelEndpoint,
    TerminalSettingsService terminalSettings,
    SkillModeDefaultsService skillModeDefaults) : SettingsControllerBase
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
        return Ok(new TerminalSettingsDto { Default_vendor = ToDtoVendor(vendor) });
    }

    public override async Task<ActionResult<TerminalSettingsDto>> SetTerminalSettings(UpdateTerminalSettingsRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var saved = await terminalSettings.SetDefaultVendorAsync(
            ToWireVendor(body.Default_vendor), HttpContext.RequestAborted);
        return Ok(new TerminalSettingsDto { Default_vendor = ToDtoVendor(saved) });
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

    private static string ToWireVendor(TerminalAgentVendor vendor) => vendor switch
    {
        TerminalAgentVendor.Claude => TerminalAgentCatalog.VendorClaude,
        TerminalAgentVendor.Codex => TerminalAgentCatalog.VendorCodex,
        TerminalAgentVendor.Opencode => TerminalAgentCatalog.VendorOpencode,
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'."),
    };

    private static TerminalAgentVendor ToDtoVendor(string vendor) => vendor switch
    {
        TerminalAgentCatalog.VendorClaude => TerminalAgentVendor.Claude,
        TerminalAgentCatalog.VendorCodex => TerminalAgentVendor.Codex,
        TerminalAgentCatalog.VendorOpencode => TerminalAgentVendor.Opencode,
        _ => throw new ArgumentOutOfRangeException(nameof(vendor), $"Unknown terminal vendor '{vendor}'."),
    };
}
