using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

public sealed class TerminalController(
    RunPreflightOrchestrator orchestrator,
    TerminalSessionStatusService statusService,
    TerminalSessionKillService killService,
    TerminalHookStatusHandler hookStatus,
    IntentTerminalPreviewHandler previewHandler,
    ILogger<TerminalController> logger) : TerminalControllerBase
{
    public override async Task<ActionResult<IntentTerminalPreviewResponse>> PreviewIntentTerminal(
        string intent_id,
        PreviewIntentTerminalRequest body)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var mode = TerminalRunResponseMapper.ToDomainMode(body.Mode);
            var preview = await previewHandler.HandleAsync(
                new IntentTerminalPreviewQuery(intent_id, mode, body.Selected_part_ids?.ToArray()),
                HttpContext.RequestAborted);
            return Ok(TerminalPreviewMapper.ToDto(intent_id, body.Mode, preview));
        }
        catch (ApiException ex)
        {
            return TerminalErrorMapper.Problem(ex);
        }
    }

    public override Task<ActionResult<RunIntentTerminalResponse>> RunIntentTerminal(
        string intent_id,
        RunIntentTerminalRequest body) =>
        ExecuteAsync(intent_id, body, restart: false);

    public override Task<ActionResult<RunIntentTerminalResponse>> RestartIntentTerminal(
        string intent_id,
        RunIntentTerminalRequest body) =>
        ExecuteAsync(intent_id, body, restart: true);

    public override async Task<ActionResult<RunIntentTerminalResponse>> GetIntentTerminalSession(string intent_id)
    {
        try
        {
            var result = await statusService.GetAsync(intent_id, HttpContext.RequestAborted);
            return Ok(TerminalRunResponseMapper.ToDto(result));
        }
        catch (ApiException ex)
        {
            return TerminalErrorMapper.Map(ex);
        }
    }

    public override async Task<ActionResult<RunIntentTerminalResponse>> KillIntentTerminal(string intent_id)
    {
        try
        {
            var result = await killService.KillAsync(intent_id, HttpContext.RequestAborted);
            return Ok(TerminalRunResponseMapper.ToDto(result));
        }
        catch (ApiException ex)
        {
            return TerminalErrorMapper.Map(ex);
        }
    }

    public override async Task<IActionResult> ReceiveIntentTerminalHook(
        string intent_id,
        Event @event,
        TerminalRunMode? mode)
    {
        TerminalEndpointLog.HookReceived(logger, intent_id, @event);
        var domainMode = mode is null ? null : TerminalRunResponseMapper.ToDomainMode(mode.Value);
        try
        {
            await hookStatus.HandleAsync(intent_id, ToHookEvent(@event), domainMode, HttpContext.RequestAborted);
        }
        catch (ApiException ex)
        {
            // Fire-and-forget callback: a status-derivation failure must not surface to the agent's
            // hook (it would noise up the session), so we log and still ack 200.
            TerminalEndpointLog.HookStatusFailed(logger, intent_id, @event, ex);
        }

        return Ok();
    }

    private static string ToHookEvent(Event @event) => @event switch
    {
        Event.Stop => TerminalHookEvents.Stop,
        Event.UserPromptSubmit => TerminalHookEvents.UserPromptSubmit,
        _ => throw new ArgumentOutOfRangeException(nameof(@event), $"Unknown terminal hook event '{@event}'."),
    };

    private async Task<ActionResult<RunIntentTerminalResponse>> ExecuteAsync(
        string intentId,
        RunIntentTerminalRequest body,
        bool restart)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var domainMode = TerminalRunResponseMapper.ToDomainMode(body.Mode);
            var launch = TerminalRunResponseMapper.ToLaunchInput(body);
            var prompt = TerminalRunResponseMapper.ToSpawnPrompt(body);
            var result = await orchestrator.RunAsync(
                intentId, domainMode, launch, prompt, restart, HttpContext.RequestAborted);
            var dto = TerminalRunResponseMapper.ToDto(result);
            return Accepted(dto);
        }
        catch (ApiException ex)
        {
            return TerminalErrorMapper.Map(ex);
        }
    }
}
