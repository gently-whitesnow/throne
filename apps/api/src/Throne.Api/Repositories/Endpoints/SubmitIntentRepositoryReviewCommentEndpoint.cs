using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>POST /api/v1/intents/{intent_id}/repositories/{binding_id}/review/comments</c>.
/// Server-side never stores the comment — provider is the source of truth.
/// </summary>
public sealed class SubmitIntentRepositoryReviewCommentEndpoint(
    RepositoryBindingResolver resolver,
    SubmitReviewWorkspaceCommentUseCase useCase
)
{
    public async Task<ActionResult<SubmittedReviewCommentDto>> RunAsync(
        string intentId,
        string bindingId,
        SubmitReviewCommentRequest body,
        CancellationToken ct
    )
    {
        ArgumentNullException.ThrowIfNull(body);
        await resolver.EnsureIntentExistsAsync(intentId, ct);
        var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
        var request = ReviewWorkspaceDtoMapper.ToSubmitRequest(body);
        var submitted = await useCase.SubmitAsync(binding, request, ct);
        var dto = ReviewWorkspaceDtoMapper.ToSubmittedDto(submitted, binding.Id.Value);
        return new ObjectResult(dto) { StatusCode = StatusCodes.Status201Created };
    }
}
