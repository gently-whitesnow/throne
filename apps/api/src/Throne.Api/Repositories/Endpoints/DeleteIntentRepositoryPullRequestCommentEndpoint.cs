using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>DELETE /api/v1/intents/{intent_id}/repositories/{binding_id}/pull-request-comments/{comment_id}</c>.
/// Server-side never stores the comment — the delete is proxied straight to the
/// provider. <c>thread_id</c> is required for GitLab note deletes (422 when absent).
/// </summary>
public sealed class DeleteIntentRepositoryPullRequestCommentEndpoint(
    RepositoryBindingResolver resolver,
    DeletePullRequestCommentUseCase useCase)
{
    public async Task<IActionResult> RunAsync(
        string intentId,
        string bindingId,
        string commentId,
        string? threadId,
        CancellationToken ct)
    {
        try
        {
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
            await useCase.DeleteAsync(binding, commentId, threadId, ct);
            return new NoContentResult();
        }
        catch (ApiException ex)
        {
            return ReviewWorkspaceErrorMapper.MapDeleteComment(ex);
        }
    }
}
