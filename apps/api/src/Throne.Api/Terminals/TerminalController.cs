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
    ILogger<TerminalController> logger
) : TerminalControllerBase
{
    public override Task<ActionResult<TerminalVendorCatalogResponse>> ListTerminalVendors() =>
        Task.FromResult<ActionResult<TerminalVendorCatalogResponse>>(Ok(TerminalVendorCatalogMapper.ToDto()));

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
    ) => ExecuteAsync(intent_id, body, restart: false);

    public override Task<ActionResult<RunIntentTerminalResponse>> RestartIntentTerminal(
        string intent_id,
        RunIntentTerminalRequest body
    ) => ExecuteAsync(intent_id, body, restart: true);

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

    public override async Task<ActionResult<TerminalHookCallbackResponse>> ReceiveIntentTerminalHook(
        string intent_id,
        Event @event,
        TerminalRunMode? mode
    )
    {
        TerminalEndpointLog.HookReceived(logger, intent_id, @event);
        var response = await hookStatusAck.HandleAsync(
            intent_id, @event, mode, HttpContext.RequestAborted);
        return Ok(response);
    }

    private async Task<ActionResult<RunIntentTerminalResponse>> ExecuteAsync(
        string intentId,
        RunIntentTerminalRequest body,
        bool restart
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var domainMode = TerminalRunResponseMapper.ToDomainMode(body.Mode);
        var launch = TerminalRunResponseMapper.ToLaunchInput(body);
        var prompt = TerminalRunResponseMapper.ToSpawnPrompt(body);
        var result = await orchestrator.RunAsync(
            intentId,
            domainMode,
            launch,
            prompt,
            restart,
            HttpContext.RequestAborted
        );
        var dto = TerminalRunResponseMapper.ToDto(result);
        return Accepted(dto);
    }
}
