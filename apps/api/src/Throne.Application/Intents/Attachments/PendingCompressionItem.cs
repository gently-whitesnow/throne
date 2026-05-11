namespace Throne.Application.Intents.Attachments;

public sealed record PendingCompressionItem(
    string AttachmentId,
    string GridFsId,
    string ContentType);
