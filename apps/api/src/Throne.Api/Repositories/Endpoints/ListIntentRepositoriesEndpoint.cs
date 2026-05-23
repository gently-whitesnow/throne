using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/intents/{intent_id}/repositories</c> (T-11).
/// Returns full <see cref="RepositoryBindingDto"/> cards; the compact projection
/// reused by MCP <c>get_intent.repositories</c> lives in T-13.
/// </summary>
public sealed class ListIntentRepositoriesEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<ICollection<RepositoryBindingDto>>> RunAsync(string intentId, CancellationToken ct)
    {
        try
        {
            var bindings = await service.ListByIntentAsync(intentId, ct);
            return new OkObjectResult(bindings.Select(RepositoryDtoMapper.ToBindingDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapListBindings(ex);
        }
    }
}
