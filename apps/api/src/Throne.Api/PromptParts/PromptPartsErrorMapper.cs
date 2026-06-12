using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;

namespace Throne.Api.PromptParts;

internal static class PromptPartsErrorMapper
{
    public static ObjectResult Problem(ApiException ex) => ex.Code switch
    {
        ErrorCodes.PromptPartNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("Prompt part not found", ex.Detail)),
        ErrorCodes.PromptPartAlreadyExists =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Prompt part already exists", ex)),
        ErrorCodes.PromptPartVersionConflict =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Prompt part version conflict", ex)),
        ErrorCodes.PromptPartTextMatchFailed =>
            new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Prompt part text match error", ex)),
        ErrorCodes.ValidationFailed =>
            new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
    };
}
