namespace Throne.Application.ChatUploads;

public sealed record ChatUploadManifest(
    int SchemaVersion,
    string Agent,
    string? AgentVersion,
    string Device,
    string? DeviceDisplayName,
    DateTimeOffset CreatedAt,
    ChatUploadDateRange DateRange,
    IReadOnlyList<ChatUploadConversation> Conversations);

public sealed record ChatUploadDateRange(DateTimeOffset From, DateTimeOffset To);

public sealed record ChatUploadConversation(
    string Id,
    string Path,
    string Sha256,
    int MessageCount,
    DateTimeOffset From,
    DateTimeOffset To,
    long SizeBytes);
