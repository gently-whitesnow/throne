using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/intents/{intent_id}/repositories/{binding_id}/pull-request/comments</c>.
/// Reads the locally persisted review-comments feed for the binding (T-10 owns the
/// upstream poll). Pagination is intentionally omitted in slice 1 — the server
/// returns the full feed ordered by <c>created_at</c> ASC.
/// </summary>
public sealed class ListIntentRepositoryPullRequestCommentsEndpoint(
    RepositoryBindingResolver resolver,
    IPullRequestCommentRepository comments)
{
    public async Task<ActionResult<ICollection<PullRequestCommentDto>>> RunAsync(
        string intentId,
        string bindingId,
        DateTimeOffset? since,
        CancellationToken ct)
    {
        try
        {
            // EnsureIntentExistsAsync gives the 404 surface for unknown intents; the
            // binding ownership check happens inside LoadBindingAsync.
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);

            if (binding.State.PullRequestNumber is null)
            {
                throw new ApiException(
                    ErrorCodes.RepositoryPullRequestNotAttached,
                    $"Binding '{binding.Id.Value}' has no pull request attached; nothing to read.",
                    new Dictionary<string, object?> { ["binding_id"] = binding.Id.Value });
            }

            var stored = await comments.ListByBindingAsync(new BindingId(bindingId), ct);
            var filtered = since is null
                ? stored
                : stored.Where(c => c.CreatedAt >= since.Value).ToList();
            return new OkObjectResult(filtered.Select(RepositoryDtoMapper.ToCommentDto).ToList());
        }
        catch (ApiException ex)
        {
            return RepositoriesErrorMapper.MapListComments(ex);
        }
    }
}
