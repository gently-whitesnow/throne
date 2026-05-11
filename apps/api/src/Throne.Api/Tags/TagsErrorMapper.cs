using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;

namespace Throne.Api.Tags;

internal static class TagsErrorMapper
{
    public static ActionResult<T> Map<T>(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.TagNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Tag not found", ex.Detail)),
            ErrorCodes.TagNameTaken =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Tag name already taken", ex)),
            ErrorCodes.TagVersionConflict =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Tag version conflict", ex)),
            ErrorCodes.TagInUse =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Tag in use", ex)),
            ErrorCodes.TagNameInvalid =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Invalid tag name", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };
}
