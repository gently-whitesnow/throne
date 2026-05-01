namespace Throne.Application.Intents;

public sealed record UploadIntentAttachmentCommand(
    string IntentId,
    Stream Content,
    string FileName,
    string ContentType,
    long ContentLength);
