namespace Throne.Application.ChatUploads;

/// <summary>
/// Inbound command for the chat-history upload endpoint. The controller passes
/// the streamed multipart parts straight through; the handler validates manifest
/// shape and per-conversation sha256, then hands persistence to the repository.
/// </summary>
public sealed record CreateChatUploadCommand(
    Stream ArchiveContent,
    long ArchiveSize,
    string ManifestJson);
