using System.Text;
using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Application.ChatUploads;

public sealed class CreateChatUploadHandler(
    IChatUploadRepository uploads,
    IUnitOfWork unitOfWork)
{
    public async Task<ChatUpload> HandleAsync(CreateChatUploadCommand command, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.ArchiveContent);

        if (command.ArchiveSize < 1)
        {
            throw new ApiException(
                ErrorCodes.ChatUploadArchiveInvalid,
                "Uploaded archive is empty.",
                new Dictionary<string, object?> { ["field"] = "archive" });
        }

        if (command.ArchiveSize > ChatUploadLimits.MaxArchiveBytes)
        {
            throw new ApiException(
                ErrorCodes.ChatUploadTooLarge,
                $"Archive exceeds maximum size of {ChatUploadLimits.MaxArchiveBytes} bytes.",
                new Dictionary<string, object?>
                {
                    ["max_bytes"] = ChatUploadLimits.MaxArchiveBytes,
                    ["content_length"] = command.ArchiveSize,
                });
        }

        if (string.IsNullOrWhiteSpace(command.ManifestJson))
        {
            throw new ApiException(
                ErrorCodes.ChatUploadManifestInvalid,
                "Manifest body is empty.",
                new Dictionary<string, object?> { ["field"] = "manifest" });
        }

        if (Encoding.UTF8.GetByteCount(command.ManifestJson) > ChatUploadLimits.MaxManifestBytes)
        {
            throw new ApiException(
                ErrorCodes.ChatUploadManifestInvalid,
                $"Manifest exceeds maximum size of {ChatUploadLimits.MaxManifestBytes} bytes.",
                new Dictionary<string, object?> { ["max_bytes"] = ChatUploadLimits.MaxManifestBytes });
        }

        var manifest = ChatUploadManifestParser.Parse(command.ManifestJson);
        ChatUploadArchiveValidator.Validate(command.ArchiveContent, manifest);

        var outcome = await unitOfWork.ExecuteOutsideTransactionAsync(
            inner => uploads.AddAsync(manifest, command.ArchiveContent, command.ArchiveSize, inner),
            ct);

        return outcome.Upload;
    }
}
