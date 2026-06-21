using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Returns full <see cref="RepositoryBindingDto"/> cards for the intent page and
/// CLI binding discovery.
/// </summary>
public sealed class ListIntentRepositoriesEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<ICollection<RepositoryBindingDto>>> RunAsync(
        string intentId,
        CancellationToken ct
    )
    {
        var bindings = await service.ListByIntentAsync(intentId, ct);
        return new OkObjectResult(bindings.Select(RepositoryDtoMapper.ToBindingDto).ToList());
    }
}
