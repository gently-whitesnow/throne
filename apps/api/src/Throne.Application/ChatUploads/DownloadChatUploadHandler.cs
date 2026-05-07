using Throne.Application.Errors;
using Throne.Application.Ports;

namespace Throne.Application.ChatUploads;

public sealed class DownloadChatUploadHandler(IChatUploadRepository uploads)
{
    public async Task<ChatUploadContent> HandleAsync(string id, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var content = await uploads.OpenContentAsync(id, ct);
        return content
            ?? throw new ApiException(
                ErrorCodes.ChatUploadNotFound,
                $"Chat upload '{id}' not found.",
                new Dictionary<string, object?> { ["chat_upload_id"] = id });
    }
}
