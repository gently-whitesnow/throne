namespace Throne.Application.Intents;

public sealed record IntentAttachment(
    string Id,
    string IntentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset CreatedAt);
