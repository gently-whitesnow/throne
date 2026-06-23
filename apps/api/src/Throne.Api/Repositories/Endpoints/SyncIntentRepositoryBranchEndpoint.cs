using Microsoft.AspNetCore.Mvc;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// «Синхронизировать ветку»: hard-syncs the local clone's current branch to its remote tip
/// (<c>git fetch</c> + <c>git reset --hard origin/{branch}</c>). Distinct from «Обновить» —
/// the working tree is rewritten and uncommitted changes are discarded. Returns the binding
/// unchanged so the row re-renders with the same metadata.
/// </summary>
public sealed class SyncIntentRepositoryBranchEndpoint(SyncRepositoryBranchUseCase useCase)
{
    public async Task<ActionResult<RepositoryBindingDto>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct
    )
    {
        var binding = await useCase.ExecuteAsync(
            new SyncRepositoryBranchCommand(intentId, bindingId),
            ct
        );
        return new OkObjectResult(RepositoryDtoMapper.ToBindingDto(binding));
    }
}
