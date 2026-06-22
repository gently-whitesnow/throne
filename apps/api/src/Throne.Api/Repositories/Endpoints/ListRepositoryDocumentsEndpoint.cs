using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>Summary cards (no markdown body) for the knowledge pages of a repository.</summary>
public sealed class ListRepositoryDocumentsEndpoint(IRepositoryArtifactRepository artifacts)
{
    public async Task<ActionResult<ICollection<RepositoryDocumentSummaryDto>>> RunAsync(
        string provider,
        string owner,
        string repo,
        CancellationToken ct
    )
    {
        var coordinate = RepositoryCoordinateFactory.Create(
            provider,
            owner,
            repo
        );
        var pages = await artifacts.ListByCoordinateAsync(coordinate, ct);
        return new OkObjectResult(
            pages.Select(RepositoryRegistryDtoMapper.ToDocumentSummaryDto).ToList()
        );
    }
}
