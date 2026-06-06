using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

public sealed class GetRepositoryEndpoint(GetRepositoryHandler handler)
{
    public async Task<ActionResult<RepositoryDto>> RunAsync(
        GitProvider provider,
        string owner,
        string repo,
        CancellationToken ct)
    {
        try
        {
            var repository = await handler.HandleAsync(
                RepositoryEnumDtoMapper.ToProviderName(provider), owner, repo, ct);
            return new OkObjectResult(RepositoryRegistryDtoMapper.ToRepositoryDto(repository));
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapRepositoryDocument<RepositoryDto>(ex);
        }
    }
}
