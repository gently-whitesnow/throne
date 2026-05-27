using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Errors;
using Throne.Application.Terminals;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

public sealed class TerminalController(RunPreflightOrchestrator orchestrator) : TerminalControllerBase
{
    public override Task<ActionResult<RunIntentTerminalResponse>> RunIntentTerminal(
        string intent_id,
        RunIntentTerminalRequest body) =>
        ExecuteAsync(intent_id, body, restart: false);

    public override Task<ActionResult<RunIntentTerminalResponse>> RestartIntentTerminal(
        string intent_id,
        RunIntentTerminalRequest body) =>
        ExecuteAsync(intent_id, body, restart: true);

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
