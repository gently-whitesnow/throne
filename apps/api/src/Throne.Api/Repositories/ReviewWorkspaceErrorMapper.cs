using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Maps Slice 4A review-workspace <see cref="ApiException"/>s to typed
/// ProblemDetails responses.
/// </summary>
internal static class ReviewWorkspaceErrorMapper
{
    public static ActionResult<PullRequestDiffDto> MapDiff(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound or ErrorCodes.RepositoryUpstreamGone =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "No pull request attached", ex)),
            ErrorCodes.ValidationFailed
                or ErrorCodes.RepositoryProviderUnsupported
                or ErrorCodes.RepositoryProviderNotAuthenticated =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<ICollection<PullRequestCommitDto>> MapCommits(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound or ErrorCodes.RepositoryUpstreamGone =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "No pull request attached", ex)),
            ErrorCodes.RepositoryProviderUnsupported or ErrorCodes.RepositoryProviderNotAuthenticated =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<ReviewThreadDto> MapResolveThread(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound or ErrorCodes.RepositoryUpstreamGone =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "No pull request attached", ex)),
            ErrorCodes.RepositoryReviewAnchorInvalid
                or ErrorCodes.RepositoryProviderUnsupported
                or ErrorCodes.RepositoryProviderNotAuthenticated
                or ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static IActionResult MapDeleteComment(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound or ErrorCodes.RepositoryUpstreamGone =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "No pull request attached", ex)),
            ErrorCodes.RepositoryReviewAnchorInvalid
                or ErrorCodes.RepositoryProviderUnsupported
                or ErrorCodes.RepositoryProviderNotAuthenticated
                or ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<SubmittedReviewCommentDto> MapSubmit(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound or ErrorCodes.RepositoryUpstreamGone =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "No pull request attached", ex)),
            ErrorCodes.RepositoryReviewAnchorInvalid
                or ErrorCodes.RepositoryProviderUnsupported
                or ErrorCodes.RepositoryProviderNotAuthenticated
                or ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };
}
