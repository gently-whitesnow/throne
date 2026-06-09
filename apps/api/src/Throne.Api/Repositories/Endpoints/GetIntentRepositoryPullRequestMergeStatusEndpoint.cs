using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/intents/{intent_id}/repositories/{binding_id}/pull-request/merge-status</c>.
/// Reads provider-neutral mergeability + checks state for the review-workspace merge control.
/// </summary>
public sealed class GetIntentRepositoryPullRequestMergeStatusEndpoint(
    RepositoryBindingResolver resolver,
    MergePullRequestUseCase useCase)
{
    public async Task<ActionResult<PullRequestMergeStatusDto>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        try
        {
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
            var status = await useCase.GetStatusAsync(binding, ct);
            return new OkObjectResult(ReviewWorkspaceDtoMapper.ToMergeStatusDto(status));
        }
        catch (ApiException ex)
        {
            return ReviewWorkspaceErrorMapper.MapMergeStatus(ex);
        }
    }
}
