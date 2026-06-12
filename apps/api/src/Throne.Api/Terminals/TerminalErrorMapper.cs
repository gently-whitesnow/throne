using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Terminal.Contracts.Generated;

namespace Throne.Api.Terminals;

internal static class TerminalErrorMapper
{
    public static ActionResult<RunIntentTerminalResponse> Map(ApiException ex) => Problem(ex);

    /// <summary>Maps an <see cref="ApiException"/> to a problem result usable by any terminal endpoint.</summary>
    public static ObjectResult Problem(ApiException ex) => ex.Code switch
    {
        ErrorCodes.IntentNotFound =>
            new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
        ErrorCodes.TerminalSessionAlreadyRunning =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Terminal session already running", ex)),
        ErrorCodes.IntentVersionConflict =>
            new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Intent version conflict", ex)),
        ErrorCodes.TerminalArgsInvalid =>
            new BadRequestObjectResult(ApiProblems.Build(StatusCodes.Status400BadRequest, "Invalid terminal launch arguments", ex)),
        ErrorCodes.CapabilityDisabled
            or ErrorCodes.TerminalModeInvalid
            or ErrorCodes.TerminalRunPreflightBlocked
            or ErrorCodes.TerminalCloneWaitTimeout
            or ErrorCodes.TerminalSpawnFailed
            or ErrorCodes.ValidationFailed
            or ErrorCodes.IntentTextMatchNotFound
            or ErrorCodes.IntentTextMatchAmbiguous
            or ErrorCodes.RepositoryProviderUnsupported
            or ErrorCodes.RepositoryProviderNotAuthenticated =>
            new UnprocessableEntityObjectResult(
                ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Run pre-flight rejected", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
    };
}
