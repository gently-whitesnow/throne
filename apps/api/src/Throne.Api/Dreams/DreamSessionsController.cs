using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Api.Shared;
using Throne.Application.Dreams;
using Throne.Application.Errors;
using Throne.Dreams.Contracts.Generated;

namespace Throne.Api.Dreams;

/// <summary>
/// HTTP controller for DreamSession (ADR-0022) read endpoints. Writes go
/// exclusively through MCP (<c>record_dream_session</c>); the UI is read-only.
/// </summary>
public sealed class DreamSessionsController(
    ListDreamSessionsHandler listHandler,
    GetDreamSessionHandler getHandler,
    GetDreamSourcesHandler sourcesHandler
) : DreamsControllerBase
{
    public override async Task<ActionResult<DreamSessionPageDto>> ListDreamSessions(
        string vendor = null!,
        string host = null!,
        int? limit = null,
        string cursor = null!
    )
    {
        var page = await listHandler.HandleAsync(
            new ListDreamSessionsQuery(vendor, host, limit, cursor),
            HttpContext.RequestAborted
        );
        return Ok(DreamSessionDtoMapper.ToPageDto(page));
    }

    public override async Task<ActionResult<DreamSessionDto>> GetDreamSession(
        string dream_session_id
    )
    {
        var session = await getHandler.HandleAsync(dream_session_id, HttpContext.RequestAborted);
        return Ok(DreamSessionDtoMapper.ToDto(session));
    }

    public override Task<ActionResult<DreamSourcePageDto>> ListDreamSources()
    {
        var entries = sourcesHandler.Handle();
        return Task.FromResult<ActionResult<DreamSourcePageDto>>(
            Ok(DreamSessionDtoMapper.ToSourcePageDto(entries))
        );
    }
}
