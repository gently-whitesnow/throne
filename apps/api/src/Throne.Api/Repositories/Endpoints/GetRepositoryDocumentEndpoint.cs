using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class GetRepositoryDocumentEndpoint(GetRepositoryDocumentHandler handler)
{
    public async Task<ActionResult<RepositoryDocumentDto>> RunAsync(
        GitProvider provider,
        string owner,
        string repo,
        string slug,
        CancellationToken ct)
    {
        try
        {
            var coordinate = RepositoryCoordinateFactory.Create(
                RepositoryEnumDtoMapper.ToProviderName(provider), owner, repo);
            var artifact = await handler.HandleAsync(new GetRepositoryDocumentQuery(coordinate, slug), ct);
            return new OkObjectResult(RepositoryRegistryDtoMapper.ToDocumentDto(artifact));
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapRepositoryDocument<RepositoryDocumentDto>(ex);
        }
    }
}
