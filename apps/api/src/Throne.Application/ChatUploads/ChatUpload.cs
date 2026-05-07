namespace Throne.Application.ChatUploads;

public sealed record ChatUpload(
    string Id,
    string OwnerUserId,
    string Agent,
    string? AgentVersion,
    string Device,
    string? DeviceDisplayName,
    DateTimeOffset DateRangeFrom,
    DateTimeOffset DateRangeTo,
    int ConversationCount,
    long SizeBytes,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record ChatUploadContent(ChatUpload Upload, Stream Content);
