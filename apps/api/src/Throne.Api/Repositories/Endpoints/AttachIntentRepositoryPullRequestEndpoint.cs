using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Attaches a pull request to an existing binding without delete/rebind (intent spec C).
/// Returns the refreshed binding; <c>intent.repository_bound</c> is fanned out for other
/// open clients via the dispatching unit-of-work.
/// </summary>
public sealed class AttachIntentRepositoryPullRequestEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<RepositoryBindingDto>> RunAsync(
        string intentId,
        string bindingId,
        AttachIntentRepositoryPullRequestRequest body,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var binding = await service.AttachPullRequestAsync(
                new AttachRepositoryPullRequestCommand(intentId, bindingId, body.Pull_request_number), ct);
            return new OkObjectResult(RepositoryDtoMapper.ToBindingDto(binding));
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapAttachPullRequest(ex);
        }
    }
}
