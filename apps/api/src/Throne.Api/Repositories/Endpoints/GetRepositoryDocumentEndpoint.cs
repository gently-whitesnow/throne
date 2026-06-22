using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class GetRepositoryDocumentEndpoint(GetRepositoryDocumentHandler handler)
{
    public async Task<ActionResult<RepositoryDocumentDto>> RunAsync(
        string provider,
        string owner,
        string repo,
        string slug,
        CancellationToken ct
    )
    {
        var coordinate = RepositoryCoordinateFactory.Create(
            provider,
            owner,
            repo
        );
        var artifact = await handler.HandleAsync(
            new GetRepositoryDocumentQuery(coordinate, slug),
            ct
        );
        return new OkObjectResult(RepositoryRegistryDtoMapper.ToDocumentDto(artifact));
    }
}
