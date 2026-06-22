using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Manual upsert of a knowledge page (ADR-0031). A domain guard rejection on the slug (a path
/// segment the contract cannot fully constrain) surfaces as 422 rather than an unhandled 500.
/// </summary>
public sealed class PutRepositoryDocumentEndpoint(RepositoryArtifactWriter writer)
{
    public async Task<ActionResult<RepositoryDocumentDto>> RunAsync(
        string provider,
        string owner,
        string repo,
        string slug,
        PutRepositoryDocumentRequest body,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var coordinate = RepositoryCoordinateFactory.Create(
                provider,
                owner,
                repo
            );
            var command = new WriteRepositoryArtifactCommand(
                coordinate,
                slug,
                body.Title,
                body.Document,
                body.Expected_version
            );
            var artifact = await writer.WriteAsync(command, ct);
            return new OkObjectResult(RepositoryRegistryDtoMapper.ToDocumentDto(artifact));
        }
        catch (ArgumentException ex)
        {
            return new UnprocessableEntityObjectResult(
                ApiProblems.Build(
                    StatusCodes.Status422UnprocessableEntity,
                    "Validation failed",
                    ex.Message
                )
            );
        }
    }
}
