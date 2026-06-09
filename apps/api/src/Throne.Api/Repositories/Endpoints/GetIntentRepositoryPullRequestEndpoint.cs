using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/intents/{intent_id}/repositories/{binding_id}/review/pull-request</c>.
/// Resolves the binding and proxies the provider-read PR header (title, state,
/// author, branches, description) through to the client. Never persisted server-side.
/// </summary>
public sealed class GetIntentRepositoryPullRequestEndpoint(
    RepositoryBindingResolver resolver,
    GetReviewWorkspacePullRequestUseCase useCase)
{
    public async Task<ActionResult<PullRequestHeaderDto>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        try
        {
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
            var snapshot = await useCase.GetAsync(binding, ct);
            return new OkObjectResult(ReviewWorkspaceDtoMapper.ToHeaderDto(snapshot));
        }
        catch (ApiException ex)
        {
            return ReviewWorkspaceErrorMapper.MapHeader(ex);
        }
    }
}
