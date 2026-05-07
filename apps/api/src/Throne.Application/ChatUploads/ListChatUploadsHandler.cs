using Throne.Application.Ports;

namespace Throne.Application.ChatUploads;

public sealed class ListChatUploadsHandler(IChatUploadRepository uploads)
{
    public Task<IReadOnlyList<ChatUpload>> HandleAsync(CancellationToken ct) => uploads.ListAsync(ct);
}
