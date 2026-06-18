using Microsoft.AspNetCore.Mvc;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class GetPullRequestArtifactEndpoint(GetPullRequestArtifactHandler handler)
{
    public async Task<ActionResult<PullRequestArtifactDto>> RunAsync(
        string bindingId,
        string type,
        CancellationToken ct)
    {
        var artifact = await handler.HandleAsync(
            new GetPullRequestArtifactQuery(new BindingId(bindingId), type),
            ct);
        return new OkObjectResult(PullRequestArtifactDtoMapper.ToDto(artifact));
    }
}
