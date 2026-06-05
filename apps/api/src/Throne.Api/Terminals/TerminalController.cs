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
    TerminalSessionKillService killService) : TerminalControllerBase
{
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

    private async Task<ActionResult<RunIntentTerminalResponse>> ExecuteAsync(
        string intentId,
        RunIntentTerminalRequest body,
        bool restart)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var domainMode = TerminalRunResponseMapper.ToDomainMode(body.Mode);
            var result = await orchestrator.RunAsync(intentId, domainMode, restart, HttpContext.RequestAborted);
            var dto = TerminalRunResponseMapper.ToDto(result);
            return Accepted(dto);
        }
        catch (ApiException ex)
        {
            return TerminalErrorMapper.Map(ex);
        }
    }
}
