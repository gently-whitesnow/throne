using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Centralised error-to-ProblemDetails projection for the repositories HTTP
/// module (T-11). Mirrors <c>IntentsErrorMapper</c> so each split controller
/// stays a thin trampoline body without conditional branching.
/// </summary>
internal static class RepositoriesErrorMapper
{
    public static ActionResult<RepositoryBindingDto> MapBind(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            ErrorCodes.RepositoryBindingAlreadyExists =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Binding already exists", ex)),
            ErrorCodes.RepositoryProviderUnsupported or ErrorCodes.RepositoryProviderNotAuthenticated =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static IActionResult MapUnbind(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<PullRequestSyncResultDto> MapSync(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound or ErrorCodes.RepositoryUpstreamGone =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached or ErrorCodes.RepositoryNotReady =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Binding not ready", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<ICollection<PullRequestCommentDto>> MapListComments(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound or ErrorCodes.RepositoryBindingNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.RepositoryPullRequestNotAttached =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "No pull request attached", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<ICollection<RepositoryBindingDto>> MapListBindings(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<ICollection<GitRepositoryRefDto>> MapSearch(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.RepositoryProviderUnsupported or ErrorCodes.RepositoryProviderNotAuthenticated =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };
}
