using Microsoft.AspNetCore.Mvc;
using Throne.Api.Generated.ChatUploads;
using Throne.Application.ChatUploads;
using Throne.Application.Errors;
using Throne.ChatUploads.Contracts.Generated;
using FileParameter = Throne.Api.Generated.ChatUploads.FileParameter;

namespace Throne.Api.ChatUploads;

public sealed class ChatUploadsController(
    ListChatUploadsHandler listHandler,
    CreateChatUploadHandler createHandler,
    DownloadChatUploadHandler downloadHandler,
    DeleteChatUploadHandler deleteHandler) : ChatUploadsControllerBase
{
    public override async Task<ActionResult<ICollection<ChatUploadDto>>> ListChatUploads()
    {
        var uploads = await listHandler.HandleAsync(HttpContext.RequestAborted);
        return Ok(uploads.Select(ChatUploadDtoMapper.ToDto).ToList());
    }

    [RequestFormLimits(MultipartBodyLengthLimit = ChatUploadLimits.MaxArchiveBytes + (4 * 1024 * 1024))]
    [RequestSizeLimit(ChatUploadLimits.MaxArchiveBytes + (4 * 1024 * 1024))]
    public override async Task<ActionResult<ChatUploadDto>> CreateChatUpload(
        FileParameter archive = default!,
        FileParameter manifest = default!)
    {
        _ = archive;
        _ = manifest;

        if (!Request.HasFormContentType)
        {
            return UnprocessableEntity(BuildProblem(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ValidationFailed,
                    "Request must be multipart/form-data.",
                    new Dictionary<string, object?> { ["content_type"] = Request.ContentType ?? string.Empty })));
        }

        var archiveFile = Request.Form.Files.GetFile("archive");
        if (archiveFile is null || archiveFile.Length < 1)
        {
            return UnprocessableEntity(BuildProblem(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ChatUploadArchiveInvalid,
                    "Multipart field \"archive\" is required and must be non-empty.",
                    new Dictionary<string, object?> { ["field"] = "archive" })));
        }

        string manifestJson;
        var manifestFile = Request.Form.Files.GetFile("manifest");
        if (manifestFile is { Length: > 0 })
        {
            await using var ms = manifestFile.OpenReadStream();
            using var reader = new StreamReader(ms, System.Text.Encoding.UTF8);
            manifestJson = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        }
        else if (Request.Form.TryGetValue("manifest", out var manifestText) && manifestText.Count > 0)
        {
            manifestJson = manifestText.ToString();
        }
        else
        {
            return UnprocessableEntity(BuildProblem(
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                new ApiException(
                    ErrorCodes.ChatUploadManifestInvalid,
                    "Multipart field \"manifest\" is required (file or text).",
                    new Dictionary<string, object?> { ["field"] = "manifest" })));
        }

        try
        {
            await using var stream = archiveFile.OpenReadStream();
            var upload = await createHandler.HandleAsync(
                new CreateChatUploadCommand(stream, archiveFile.Length, manifestJson),
                HttpContext.RequestAborted);

            var location = $"/api/v1/chat-uploads/{Uri.EscapeDataString(upload.Id)}";
            return Created(location, ChatUploadDtoMapper.ToDto(upload));
        }
        catch (ApiException ex)
        {
            return MapCreateError(ex);
        }
    }

    public override async Task<IActionResult> DownloadChatUpload(string id)
    {
        try
        {
            var content = await downloadHandler.HandleAsync(id, HttpContext.RequestAborted);
            return File(content.Content, "application/zip", $"{content.Upload.Id}.zip");
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.ChatUploadNotFound)
        {
            return NotFound(NotFoundProblem("Chat upload not found", ex.Detail));
        }
    }

    public override async Task<IActionResult> DeleteChatUpload(string id)
    {
        try
        {
            await deleteHandler.HandleAsync(id, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (ApiException ex) when (ex.Code == ErrorCodes.ChatUploadNotFound)
        {
            return NotFound(NotFoundProblem("Chat upload not found", ex.Detail));
        }
    }

    private ActionResult<ChatUploadDto> MapCreateError(ApiException ex) => ex.Code switch
    {
        ErrorCodes.ChatUploadTooLarge => StatusCode(
            StatusCodes.Status413PayloadTooLarge,
            BuildProblem(StatusCodes.Status413PayloadTooLarge, "Archive too large", ex)),
        ErrorCodes.ChatUploadManifestInvalid
            or ErrorCodes.ChatUploadSchemaUnsupported
            or ErrorCodes.ChatUploadArchiveInvalid
            or ErrorCodes.ValidationFailed => UnprocessableEntity(
                BuildProblem(StatusCodes.Status422UnprocessableEntity, "Validation failed", ex)),
        _ => throw new InvalidOperationException($"Unexpected API error code: {ex.Code}.", ex),
    };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails NotFoundProblem(string title, string detail) => new()
    {
        Type = "about:blank",
        Title = title,
        Status = StatusCodes.Status404NotFound,
        Detail = detail,
    };

    private static Microsoft.AspNetCore.Mvc.ProblemDetails BuildProblem(int status, string title, ApiException ex)
    {
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "about:blank",
            Title = title,
            Status = status,
            Detail = ex.Detail,
        };
        problem.Extensions["code"] = ex.Code;
        foreach (var (key, value) in ex.Extensions)
        {
            problem.Extensions[key] = value;
        }
        return problem;
    }
}
