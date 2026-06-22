using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// 201 Created carries the freshly persisted binding in <c>pending</c> state;
/// the background clone service drives the rest of the lifecycle.
/// </summary>
public sealed class BindIntentRepositoryEndpoint(RepositoryBindingService service)
{
    public async Task<ActionResult<RepositoryBindingDto>> RunAsync(
        string intentId,
        BindIntentRepositoryRequest body,
        IUrlHelper url,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        var command = new BindRepositoryCommand(
            IntentId: intentId,
            Provider: body.Provider,
            Owner: body.Owner,
            Repo: body.Repo,
            Host: body.Host,
            ProjectId: body.Project_id,
            DefaultBranch: body.Default_branch,
            PullRequestNumber: body.Pull_request_number
        );

        var binding = await service.BindAsync(command, ct);
        var dto = RepositoryDtoMapper.ToBindingDto(binding);
        var location = url.Action(
            action: "ListIntentRepositories",
            values: new { intent_id = binding.IntentId.Value }
        );
        return new CreatedResult(location, dto);
    }
}
