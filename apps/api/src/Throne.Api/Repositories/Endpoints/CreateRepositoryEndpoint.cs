using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>201 when the coordinate is newly registered, 200 when it already existed (idempotent).</summary>
public sealed class CreateRepositoryEndpoint(CreateRepositoryHandler handler)
{
    public async Task<ActionResult<RepositoryDto>> RunAsync(
        CreateRepositoryRequest body,
        IUrlHelper url,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        var provider = RepositoryEnumDtoMapper.ToProviderName(body.Provider);
        try
        {
            var result = await handler.HandleAsync(provider, body.Owner, body.Repo, ct);
            var dto = RepositoryRegistryDtoMapper.ToRepositoryDto(result.Repository);
            if (!result.Created)
            {
                return new OkObjectResult(dto);
            }

            var location = url.Action(
                action: "GetRepository",
                values: new { provider, owner = dto.Owner, repo = dto.Repo });
            return new CreatedResult(location, dto);
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapRepositoryDocument<RepositoryDto>(ex);
        }
    }
}
