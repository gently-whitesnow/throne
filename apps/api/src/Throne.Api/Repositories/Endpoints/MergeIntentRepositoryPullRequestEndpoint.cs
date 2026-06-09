using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>POST /api/v1/intents/{intent_id}/repositories/{binding_id}/pull-request/merge</c>.
/// Merges the binding's PR/MR at the provider; the merged lifecycle state lands on the
/// next PR sync (Throne keeps no local merge state).
/// </summary>
public sealed class MergeIntentRepositoryPullRequestEndpoint(
    RepositoryBindingResolver resolver,
    MergePullRequestUseCase useCase)
{
    public async Task<ActionResult<MergePullRequestResultDto>> RunAsync(
        string intentId,
        string bindingId,
        MergePullRequestRequest body,
        CancellationToken ct)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(body);
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
            var request = ReviewWorkspaceDtoMapper.ToMergeRequest(body);
            var result = await useCase.MergeAsync(binding, request, ct);
            return new OkObjectResult(ReviewWorkspaceDtoMapper.ToMergeResultDto(result));
        }
        catch (ApiException ex)
        {
            return ReviewWorkspaceErrorMapper.MapMerge(ex);
        }
    }
}
