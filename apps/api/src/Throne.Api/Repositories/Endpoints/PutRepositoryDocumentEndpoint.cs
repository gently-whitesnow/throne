using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Manual upsert of a knowledge page. <c>render_hint</c> is derived from the slug (ADR-0031),
/// never taken from the wire. A domain guard rejection on the slug (a path segment the contract
/// cannot fully constrain) surfaces as 422 rather than an unhandled 500.
/// </summary>
public sealed class PutRepositoryDocumentEndpoint(RepositoryArtifactWriter writer)
{
    public async Task<ActionResult<RepositoryDocumentDto>> RunAsync(
        GitProvider provider,
        string owner,
        string repo,
        string slug,
        PutRepositoryDocumentRequest body,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var coordinate = RepositoryCoordinateFactory.Create(
                RepositoryEnumDtoMapper.ToProviderName(provider), owner, repo);
            var command = new WriteRepositoryArtifactCommand(
                coordinate,
                slug,
                body.Title,
                body.Document,
                RepositoryArtifactRenderHints.ForSlug(slug),
                body.Expected_version);
            var artifact = await writer.WriteAsync(command, ct);
            return new OkObjectResult(RepositoryRegistryDtoMapper.ToDocumentDto(artifact));
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapRepositoryDocument<RepositoryDocumentDto>(ex);
        }
        catch (ArgumentException ex)
        {
            return new UnprocessableEntityObjectResult(
                ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex.Message));
        }
    }
}
