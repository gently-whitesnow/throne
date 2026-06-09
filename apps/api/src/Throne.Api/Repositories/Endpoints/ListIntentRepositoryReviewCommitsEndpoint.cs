using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/intents/{intent_id}/repositories/{binding_id}/review/commits</c>.
/// </summary>
public sealed class ListIntentRepositoryReviewCommitsEndpoint(
    RepositoryBindingResolver resolver,
    ListReviewWorkspaceCommitsUseCase useCase)
{
    public async Task<ActionResult<ICollection<PullRequestCommitDto>>> RunAsync(
        string intentId,
        string bindingId,
        CancellationToken ct)
    {
        try
        {
            await resolver.EnsureIntentExistsAsync(intentId, ct);
            var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
            var commits = await useCase.ListByBindingAsync(binding, ct);
            var dtos = commits
                .OrderBy(c => c.CommittedAt)
                .Select(ReviewWorkspaceDtoMapper.ToCommitDto)
                .ToList();
            return new OkObjectResult(dtos);
        }
        catch (ApiException ex)
        {
            return ReviewWorkspaceErrorMapper.MapCommits(ex);
        }
    }
}
