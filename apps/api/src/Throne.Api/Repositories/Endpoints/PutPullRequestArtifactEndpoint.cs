using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class PutPullRequestArtifactEndpoint(IPullRequestArtifactSink sink)
{
    public async Task<ActionResult<PullRequestArtifactDto>> RunAsync(
        string bindingId,
        string type,
        PutPullRequestArtifactRequest body,
        CancellationToken ct)
    {
        // A null body means the JSON failed to bind to the schema (e.g. an unknown enum value);
        // surface it as a 422 rather than letting a NRE become a 500.
        if (body is null)
        {
            return new UnprocessableEntityObjectResult(
                ApiProblems.Build(
                    StatusCodes.Status422UnprocessableEntity,
                    "Validation failed",
                    "Request body is missing or does not match the artifact schema."));
        }
        try
        {
            var command = new WritePullRequestArtifactCommand(
                new BindingId(bindingId),
                type,
                RepositoryEnumDtoMapper.ToDomainArtifactRender(body.Render),
                body.Content,
                body.Summary,
                RepositoryEnumDtoMapper.ToDomainArtifactSource(body.Source),
                body.Source_refs.ToList(),
                body.Produced_at,
                body.Head_sha,
                ReviewRecommendationContentJson.Serialize(body.Review_recommendation));
            var artifact = await sink.IngestAsync(command, ct);
            return new OkObjectResult(PullRequestArtifactDtoMapper.ToDto(artifact));
        }
        catch (ArgumentException ex)
        {
            return new UnprocessableEntityObjectResult(
                ApiProblems.Build(
                    StatusCodes.Status422UnprocessableEntity,
                    "Validation failed",
                    ex.Message));
        }
    }
}
