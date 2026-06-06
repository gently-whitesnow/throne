using Microsoft.AspNetCore.Mvc;
using Throne.Application.Ports;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>Metadata-only registry rows; knowledge pages are fetched per repository.</summary>
public sealed class ListRepositoriesEndpoint(IRepositoryRegistry registry)
{
    public async Task<ActionResult<ICollection<RepositoryDto>>> RunAsync(CancellationToken ct)
    {
        var repositories = await registry.ListAsync(ct);
        return new OkObjectResult(repositories.Select(RepositoryRegistryDtoMapper.ToRepositoryDto).ToList());
    }
}
