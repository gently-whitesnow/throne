using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Instructions.Contracts.Generated;

namespace Throne.Api.Instructions;

internal static class InstructionsErrorMapper
{
    public static ActionResult<InstructionDetailDto> MapReplace(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.InstructionNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Instruction not found", ex.Detail)),
            ErrorCodes.InstructionVersionConflict =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Instruction version conflict", ex)),
            ErrorCodes.InstructionTextMatchNotFound or ErrorCodes.InstructionTextMatchAmbiguous =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Instruction text match error", ex)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw ex,
        };

    public static ActionResult<InstructionDetailDto> MapCreate(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.InstructionAlreadyExists =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Instruction already exists", ex)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw ex,
        };
}
