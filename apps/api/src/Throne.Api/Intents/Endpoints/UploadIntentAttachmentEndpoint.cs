using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Throne.Api.Shared;
using Throne.Application.Errors;
using Throne.Application.Intents;
using Throne.Intents.Contracts.Generated;

namespace Throne.Api.Intents;

public sealed class UploadIntentAttachmentEndpoint(UploadIntentAttachmentHandler handler)
{
    public Task<ActionResult<IntentAttachmentDto>> RunAsync(string id, HttpRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.HasFormContentType)
        {
            return Task.FromResult<ActionResult<IntentAttachmentDto>>(new UnprocessableEntityObjectResult(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ValidationFailed,
                    "Request must be multipart/form-data.",
                    new Dictionary<string, object?> { ["content_type"] = request.ContentType ?? string.Empty }))));
        }

        var formFile = request.Form.Files.GetFile("file");
        if (formFile is null || formFile.Length < 1)
        {
            return Task.FromResult<ActionResult<IntentAttachmentDto>>(new UnprocessableEntityObjectResult(ApiProblems.Build(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ValidationFailed,
                    "Multipart field \"file\" is required and must be non-empty.",
                    new Dictionary<string, object?> { ["field"] = "file" }))));
        }

        return UploadCoreAsync(id, formFile, cancellationToken);
    }

    private async Task<ActionResult<IntentAttachmentDto>> UploadCoreAsync(string id, IFormFile formFile, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = formFile.OpenReadStream();
            var attachment = await handler.HandleAsync(
                new UploadIntentAttachmentCommand(
                    id,
                    stream,
                    formFile.FileName,
                    formFile.ContentType ?? "application/octet-stream",
                    formFile.Length),
                cancellationToken);

            var location = $"/api/v1/intents/{Uri.EscapeDataString(id)}/attachments/{Uri.EscapeDataString(attachment.Id)}";
            return new CreatedResult(location, IntentDtoMapper.ToAttachmentDto(attachment));
        }
        catch (ApiException ex)
        {
            return IntentsErrorMapper.MapUploadAttachment(ex);
        }
    }
}
