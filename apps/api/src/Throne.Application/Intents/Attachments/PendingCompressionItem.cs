namespace Throne.Application.Intents.Attachments;

public sealed record PendingCompressionItem(
    string AttachmentId,
    string ContentId,
    string ContentType);
