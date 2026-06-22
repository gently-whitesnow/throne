using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

public sealed class TerminalController(
    RunPreflightOrchestrator orchestrator,
    TerminalSessionStatusService statusService,
    TerminalSessionKillService killService,
    TerminalHookStatusAck hookStatusAck,
    IntentTerminalPreviewHandler previewHandler,
    AttachIntentTerminalSkillsHandler attachHandler,
    TerminalVendorCatalogMapper vendorCatalogMapper,
    ILogger<TerminalController> logger
) : TerminalControllerBase
{
    public override async Task<ActionResult<Throne.Terminal.Contracts.Generated.AttachIntentTerminalSkillsResponse>> AttachIntentTerminalSkills(
        string intent_id,
        Throne.Terminal.Contracts.Generated.AttachIntentTerminalSkillsRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var skillIds = body.Skill_ids?.ToArray() ?? Array.Empty<string>();
        var result = await attachHandler.HandleAsync(
            new Application.Terminals.AttachIntentTerminalSkillsRequest(intent_id, skillIds),
            HttpContext.RequestAborted);
        return Ok(new Throne.Terminal.Contracts.Generated.AttachIntentTerminalSkillsResponse
        {
            Attached_skill_ids = result.AttachedSkillIds.ToList(),
        });
    }

    public override async Task<ActionResult<TerminalVendorCatalogResponse>> ListTerminalVendors()
    {
        var dto = await vendorCatalogMapper.ToDtoAsync(HttpContext.RequestAborted);
        return Ok(dto);
    }

    public override async Task<ActionResult<IntentTerminalPreviewResponse>> PreviewIntentTerminal(
        string intent_id,
        PreviewIntentTerminalRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var mode = TerminalRunResponseMapper.ToDomainMode(body.Mode);
        var preview = await previewHandler.HandleAsync(
            new IntentTerminalPreviewQuery(intent_id, mode, body.Selected_part_ids?.ToArray()),
            HttpContext.RequestAborted
        );
        return Ok(TerminalPreviewMapper.ToDto(intent_id, body.Mode, preview));
    }

    public override Task<ActionResult<RunIntentTerminalResponse>> RunIntentTerminal(
        string intent_id,
        RunIntentTerminalRequest body
    ) => ExecuteAsync(intent_id, body);

    public override async Task<ActionResult<RunIntentTerminalResponse>> GetIntentTerminalSession(
        string intent_id
    )
    {
        var result = await statusService.GetAsync(intent_id, HttpContext.RequestAborted);
        return Ok(TerminalRunResponseMapper.ToDto(result));
    }

    public override async Task<ActionResult<RunIntentTerminalResponse>> KillIntentTerminal(
        string intent_id
    )
    {
        var result = await killService.KillAsync(intent_id, HttpContext.RequestAborted);
        return Ok(TerminalRunResponseMapper.ToDto(result));
    }

    public override async Task<IActionResult> ReceiveIntentTerminalHook(
        string intent_id,
        Event @event,
        TerminalRunMode? mode
    )
    {
        TerminalEndpointLog.HookReceived(logger, intent_id, @event);
        await hookStatusAck.HandleAsync(intent_id, @event, mode, HttpContext.RequestAborted);
        return Ok();
    }

    private async Task<ActionResult<RunIntentTerminalResponse>> ExecuteAsync(
        string intentId,
        RunIntentTerminalRequest body
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var domainMode = TerminalRunResponseMapper.ToDomainMode(body.Mode);
        var launch = TerminalRunResponseMapper.ToLaunchInput(body);
        var prompt = TerminalRunResponseMapper.ToSpawnPrompt(body);
        var reviewBindingId = TerminalRunResponseMapper.ToReviewBindingId(body);
        var result = await orchestrator.RunAsync(
            intentId,
            domainMode,
            launch,
            prompt,
            TerminalRunResponseMapper.ToSelectedSkillIds(body),
            HttpContext.RequestAborted,
            reviewBindingId
        );
        var dto = TerminalRunResponseMapper.ToDto(result);
        return Accepted(dto);
    }
}
