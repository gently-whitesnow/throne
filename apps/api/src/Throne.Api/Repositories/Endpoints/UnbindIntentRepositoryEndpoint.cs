using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Deletes the binding and its on-disk workspace directory. Idempotent — a missing
/// binding still answers 204 per the OpenAPI contract. A failed directory delete
/// surfaces as 500 with the binding left intact for a retry.
/// </summary>
public sealed class UnbindIntentRepositoryEndpoint(RepositoryBindingService service)
{
    public async Task<IActionResult> RunAsync(string intentId, string bindingId, CancellationToken ct)
    {
        try
        {
            await service.UnbindAsync(new UnbindRepositoryCommand(intentId, bindingId), ct);
            return new NoContentResult();
        }
        catch (ApiException ex) when (ex.Code is ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound)
        {
            // Contract treats unbind as idempotent — 204 even when the binding is absent.
            return new NoContentResult();
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapUnbind(ex);
        }
    }
}
