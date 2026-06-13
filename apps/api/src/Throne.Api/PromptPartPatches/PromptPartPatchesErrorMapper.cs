using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.PromptPartPatches.Contracts.Generated;

namespace Throne.Api.PromptPartPatches;

internal static class PromptPartPatchesErrorMapper
{
    public static ActionResult<PromptPartPatchDto> MapDecision(ApiException ex) => ex.Code switch
    {
        ErrorCodes.PromptPartPatchNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("PromptPartPatch not found", ex.Detail)),
        ErrorCodes.PromptPartNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("Prompt part not found", ex.Detail)),
        ErrorCodes.PromptPartPatchAlreadyDecided =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "PromptPartPatch already decided", ex)),
        ErrorCodes.PromptPartPatchNeedsRebase =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "PromptPartPatch needs rebase", ex)),
        ErrorCodes.PromptPartTextMatchFailed =>
            new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Prompt part text match error", ex)),
        ErrorCodes.ValidationFailed =>
            new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error: {ex.Code}", ex),
    };
}
