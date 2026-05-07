using Throne.Application.ChatUploads;

namespace Throne.Application.Ports;

public interface IChatUploadRepository
{
    Task<IReadOnlyList<ChatUpload>> ListAsync(CancellationToken ct);

    Task<CreateChatUploadOutcome> AddAsync(
        ChatUploadManifest manifest,
        Stream archiveContent,
        long archiveSize,
        CancellationToken ct);

    Task<ChatUploadContent?> OpenContentAsync(string id, CancellationToken ct);

    Task<DeleteChatUploadOutcome> DeleteAsync(string id, CancellationToken ct);
}
