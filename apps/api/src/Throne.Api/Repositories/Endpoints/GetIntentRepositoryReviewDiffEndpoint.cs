using Microsoft.AspNetCore.Mvc;
using Throne.Application.Errors;
using Throne.Application.Repositories;
using Throne.Repositories.Contracts.Generated;
using ReviewDiffScope = Throne.Application.Repositories.ReviewDiffScope;

namespace Throne.Api.Repositories.Endpoints;

/// <summary>
/// Backs <c>GET /api/v1/intents/{intent_id}/repositories/{binding_id}/review/diff</c>.
/// Resolves the binding, picks a scope (request / commit) and proxies the
/// provider-derived diff through to the client. Server-side never persists the
/// diff (Slice 4A).
/// </summary>
public sealed class GetIntentRepositoryReviewDiffEndpoint(
    RepositoryBindingResolver resolver,
    GetReviewWorkspaceDiffUseCase useCase
)
{
    public async Task<ActionResult<PullRequestDiffDto>> RunAsync(
        string intentId,
        string bindingId,
        ReviewDiffScope? scope,
        string? commitSha,
        CancellationToken ct
    )
    {
        await resolver.EnsureIntentExistsAsync(intentId, ct);
        var binding = await resolver.LoadBindingAsync(intentId, bindingId, ct);
        var effectiveScope = scope ?? ReviewDiffScope.Request;
        EnsureCommitSha(effectiveScope, commitSha);
        var diff =
            effectiveScope == ReviewDiffScope.Request
                ? await useCase.GetRequestDiffAsync(binding, ct)
                : await useCase.GetCommitDiffAsync(binding, commitSha!, ct);
        return new OkObjectResult(ReviewWorkspaceDtoMapper.ToDiffDto(diff));
    }

    private static void EnsureCommitSha(ReviewDiffScope scope, string? commitSha)
    {
        if (scope == ReviewDiffScope.Commit && string.IsNullOrWhiteSpace(commitSha))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed,
                "Query parameter 'commit_sha' is required when scope=commit."
            );
        }
    }
}
