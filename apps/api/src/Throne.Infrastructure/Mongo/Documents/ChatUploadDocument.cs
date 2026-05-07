using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class ChatUploadDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("owner_user_id")]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonElement("agent")]
    public string Agent { get; set; } = string.Empty;

    [BsonElement("agent_version")]
    [BsonIgnoreIfNull]
    public string? AgentVersion { get; set; }

    [BsonElement("device")]
    public string Device { get; set; } = string.Empty;

    [BsonElement("device_display_name")]
    [BsonIgnoreIfNull]
    public string? DeviceDisplayName { get; set; }

    [BsonElement("date_range_from")]
    public DateTime DateRangeFrom { get; set; }

    [BsonElement("date_range_to")]
    public DateTime DateRangeTo { get; set; }

    [BsonElement("conversation_count")]
    public int ConversationCount { get; set; }

    [BsonElement("size_bytes")]
    public long SizeBytes { get; set; }

    [BsonElement("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
