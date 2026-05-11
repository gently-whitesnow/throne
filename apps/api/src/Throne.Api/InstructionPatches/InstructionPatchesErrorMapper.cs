using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.InstructionPatches.Contracts.Generated;

namespace Throne.Api.InstructionPatches;

internal static class InstructionPatchesErrorMapper
{
    public static ActionResult<InstructionPatchDto> MapDecision(ApiException ex) => ex.Code switch
    {
        ErrorCodes.InstructionPatchNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("InstructionPatch not found", ex.Detail)),
        ErrorCodes.InstructionNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("Instruction not found", ex.Detail)),
        ErrorCodes.InstructionPatchAlreadyDecided =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "InstructionPatch already decided", ex)),
        ErrorCodes.InstructionPatchNeedsRebase =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "InstructionPatch needs rebase", ex)),
        ErrorCodes.ValidationFailed =>
            new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error: {ex.Code}", ex),
    };
}
