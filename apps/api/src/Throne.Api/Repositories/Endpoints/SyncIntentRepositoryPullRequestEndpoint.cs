using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Synchronous per ADR-0024 § 6: blocks for the upstream <c>gh api</c>
/// round-trip and returns the freshly-observed comments. Background fanout for
/// other open clients is handled by the dispatching unit-of-work.
/// </summary>
public sealed class SyncIntentRepositoryPullRequestEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<PullRequestSyncResultDto>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct
    )
    {
        var result = await service.SyncPullRequestAsync(
            new SyncRepositoryPullRequestCommand(intentId, bindingId),
            ct
        );
        return new OkObjectResult(RepositoryDtoMapper.ToSyncResultDto(result));
    }
}
