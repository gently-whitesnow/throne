using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>POST /api/v1/intents/{intent_id}/repositories/{binding_id}/sync</c>.
/// Synchronous per parent Q5 / ADR-0024 § 6: blocks for the upstream <c>gh api</c>
/// round-trip and returns the freshly-observed comments. Background fanout for
/// other open clients is handled by the dispatching unit-of-work + T-12 emitters.
/// </summary>
public sealed class SyncIntentRepositoryPullRequestEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<PullRequestSyncResultDto>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        try
        {
            var result = await service.SyncPullRequestAsync(
                new SyncRepositoryPullRequestCommand(intentId, bindingId), ct);
            return new OkObjectResult(RepositoryDtoMapper.ToSyncResultDto(result));
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapSync(ex);
        }
    }
}
