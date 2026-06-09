using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// «Обновить» disk-recovery (ADR-0024): re-clones a binding whose local workspace folder is
/// missing. Returns the binding (re-queued to <c>pending</c>/<c>cloning</c> when the folder was
/// gone, unchanged otherwise); the UI then follows <c>intent.repository_clone_progress</c>.
/// </summary>
public sealed class RefreshIntentRepositoryEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<RepositoryBindingDto>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        try
        {
            var binding = await service.RefreshAsync(
                new RefreshRepositoryBindingCommand(intentId, bindingId), ct);
            return new OkObjectResult(RepositoryDtoMapper.ToBindingDto(binding));
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapRefresh(ex);
        }
    }
}
