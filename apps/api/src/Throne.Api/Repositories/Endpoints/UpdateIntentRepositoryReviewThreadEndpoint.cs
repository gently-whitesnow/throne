using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>PATCH /api/v1/intents/{intent_id}/repositories/{binding_id}/pull-request-threads/{thread_id}</c>.
/// Toggles the thread's resolution state at the provider and echoes the state read
/// back from its response — Throne keeps no local status.
/// </summary>
public sealed class UpdateIntentRepositoryReviewThreadEndpoint(
    RepositoryBindingResolver resolver,
    ResolveReviewThreadUseCase useCase)
{
    public async Task<ActionResult<ReviewThreadDto>> RunAsync(
        string intentId,
        string bindingId,
        string threadId,
        UpdateReviewThreadRequest body,
        CancellationToken ct)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(body);
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
            var state = await useCase.ResolveAsync(binding, threadId, body.Resolved, ct);
            return new OkObjectResult(ReviewWorkspaceDtoMapper.ToThreadDto(state));
        }
        catch (ApiException ex)
        {
            return ReviewWorkspaceErrorMapper.MapResolveThread(ex);
        }
    }
}
