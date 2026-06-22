using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Version-history timeline of a page. The existing document handler resolves the page (and
/// raises the 404), then the append-only version snapshots are read by artifact id.
/// </summary>
public sealed class ListRepositoryDocumentVersionsEndpoint(
    GetRepositoryDocumentHandler getDocument,
    IRepositoryArtifactRepository artifacts
)
{
    public async Task<ActionResult<ICollection<RepositoryDocumentVersionDto>>> RunAsync(
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
        var artifact = await getDocument.HandleAsync(
            new GetRepositoryDocumentQuery(coordinate, slug),
            ct
        );
        var versions = await artifacts.ListVersionsAsync(artifact.Id, ct);
        return new OkObjectResult(
            versions.Select(RepositoryRegistryDtoMapper.ToDocumentVersionDto).ToList()
        );
    }
}
