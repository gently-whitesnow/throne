using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents.Linking;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

internal static class IntentsErrorMapper
{
    public static ActionResult<IntentDetailDto> MapReplace(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            ErrorCodes.IntentVersionConflict =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Intent version conflict", ex)),
            ErrorCodes.IntentTextMatchNotFound or ErrorCodes.IntentTextMatchAmbiguous =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Intent text match error", ex)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw ex,
        };

    public static ActionResult<IntentDetailDto> MapSetTags(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            ErrorCodes.IntentVersionConflict =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Intent version conflict", ex)),
            ErrorCodes.TagNameInvalid =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Invalid tag name", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<IntentDetailDto> MapSetStatus(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            ErrorCodes.IntentVersionConflict =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Intent state conflict", ex)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<IntentDetailDto> MapMove(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<IntentDetailDto> MapPin(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Not found", ex.Detail)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<IntentLinkDto> MapCreateLink(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            LinkErrorCodes.Duplicate =>
                new ConflictObjectResult(ApiProblems.Build(StatusCodes.Status409Conflict, "Link already exists", ex)),
            LinkErrorCodes.SelfLink or LinkErrorCodes.TypeUnsupported =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Invalid link", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };

    public static ActionResult<IntentAttachmentDto> MapUploadAttachment(ApiException ex) =>
        ex.Code switch
        {
            ErrorCodes.IntentNotFound =>
                new NotFoundObjectResult(ApiProblems.NotFound("Intent not found", ex.Detail)),
            ErrorCodes.IntentAttachmentTooLarge =>
                new ObjectResult(ApiProblems.Build(StatusCodes.Status413PayloadTooLarge, "File too large", ex))
                {
                    StatusCode = StatusCodes.Status413PayloadTooLarge,
                },
            ErrorCodes.IntentAttachmentLimitExceeded =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Too many attachments", ex)),
            ErrorCodes.ValidationFailed =>
                new UnprocessableEntityObjectResult(ApiProblems.Build(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
            _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
        };
}
