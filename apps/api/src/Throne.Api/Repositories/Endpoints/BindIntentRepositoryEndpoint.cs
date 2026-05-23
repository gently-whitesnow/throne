using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>POST /api/v1/intents/{intent_id}/repositories</c> (T-11).
/// 201 Created carries the freshly persisted binding in <c>pending</c> state;
/// background clone (T-09) drives the rest of the lifecycle.
/// </summary>
public sealed class BindIntentRepositoryEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<RepositoryBindingDto>> RunAsync(
        string intentId,
        BindIntentRepositoryRequest body,
        IUrlHelper url,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        try
        {
            var command = new BindRepositoryCommand(
                IntentId: intentId,
                Provider: RepositoryEnumDtoMapper.ToProviderName(body.Provider),
                Owner: body.Owner,
                Repo: body.Repo,
                DefaultBranch: body.Default_branch,
                PullRequestNumber: body.Pull_request_number == 0 ? null : body.Pull_request_number);

            var binding = await service.BindAsync(command, ct);
            var dto = RepositoryDtoMapper.ToBindingDto(binding);
            var location = url.Action(
                action: "ListIntentRepositories",
                values: new { intent_id = binding.IntentId.Value });
            return new CreatedResult(location, dto);
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapBind(ex);
        }
    }

}
