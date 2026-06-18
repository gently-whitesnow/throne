using Microsoft.AspNetCore.Mvc;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class ListPullRequestArtifactsEndpoint(ListPullRequestArtifactsHandler handler)
{
    public async Task<ActionResult<ICollection<PullRequestArtifactDto>>> RunAsync(
        string bindingId,
        CancellationToken ct)
    {
        var artifacts = await handler.HandleAsync(
            new ListPullRequestArtifactsQuery(new BindingId(bindingId)),
            ct);
        return new OkObjectResult(artifacts.Select(PullRequestArtifactDtoMapper.ToDto).ToList());
    }
}
