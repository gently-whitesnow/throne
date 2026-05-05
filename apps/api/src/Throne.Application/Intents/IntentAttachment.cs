namespace Throne.Application.Intents;

public sealed record IntentAttachment(
    string Id,
    string OwnerUserId,
    string IntentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);

public sealed record IntentAttachmentContent(IntentAttachment Attachment, Stream Content);
