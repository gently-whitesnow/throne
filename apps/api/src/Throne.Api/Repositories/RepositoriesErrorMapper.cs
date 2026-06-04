using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

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
            ErrorCodes.RepositoryWorkspaceRemovalFailed =>
                new ObjectResult(ApiProblems.Build(StatusCodes.Status500InternalServerError, "Workspace removal failed", ex))
                {
                    StatusCode = StatusCodes.Status500InternalServerError,
                },
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

    public static ActionResult<ICollection<GitBranchRefDto>> MapListBranches(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.RepositoryProviderUnsupported or ErrorCodes.RepositoryProviderNotAuthenticated =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<ICollection<GitPullRequestRefDto>> MapListPullRequests(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.RepositoryProviderUnsupported or ErrorCodes.RepositoryProviderNotAuthenticated =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };
}
