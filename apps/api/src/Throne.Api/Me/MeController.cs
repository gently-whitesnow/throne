using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated;
using Throne.Application.Auth;
using Throne.Application.Ports;
using Throne.Me.Contracts.Generated;

[assembly: SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Scope = "namespace",
    Target = "~N:Throne.Api.Me",
    Justification = "Module namespace mirrors REST module name '/api/v1/me' (ADR-0006).")]

namespace Throne.Api.Me;

public sealed class MeController(
    GenerateMcpTokenHandler generateHandler,
    GetMcpTokenMetaHandler getMetaHandler,
    IUnitOfWork unitOfWork) : MeControllerBase
{
    public override async Task<ActionResult<McpTokenMetaDto>> GetMcpToken()
    {
        var meta = await getMetaHandler.HandleAsync(HttpContext.RequestAborted);
        return Ok(MapMeta(meta));
    }

    public override async Task<ActionResult<McpTokenIssuedDto>> RegenerateMcpToken()
    {
        var result = await unitOfWork.ExecuteAsync(
            inner => generateHandler.HandleAsync(inner),
            HttpContext.RequestAborted);

        return CreatedAtAction(
            nameof(GetMcpToken),
            null,
            new McpTokenIssuedDto
            {
                Token = result.Plaintext,
                Created_at = result.CreatedAt,
                Last_four = result.LastFour,
            });
    }

    private static McpTokenMetaDto MapMeta(McpTokenMeta meta) => new()
    {
        Has_token = meta.HasToken,
        Created_at = meta.CreatedAt ?? default,
        Last_four = meta.LastFour ?? string.Empty,
    };
}
