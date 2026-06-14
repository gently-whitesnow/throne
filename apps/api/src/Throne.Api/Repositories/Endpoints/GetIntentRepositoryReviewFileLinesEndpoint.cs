using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class GetIntentRepositoryReviewFileLinesEndpoint(
    RepositoryBindingResolver resolver,
    GetRepositoryFileLinesUseCase useCase
)
{
    public async Task<ActionResult<ReviewFileLinesDto>> RunAsync(
        string intentId,
        string bindingId,
        string sha,
        string path,
        int from,
        int to,
        CancellationToken ct
    )
    {
        await resolver.EnsureIntentExistsAsync(intentId, ct);
        var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
        var lines = await useCase.GetAsync(binding, sha, path, from, to, ct);
        return new OkObjectResult(ReviewWorkspaceDtoMapper.ToFileLinesDto(lines));
    }
}
