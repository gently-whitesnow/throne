using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class McpCallLogDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("session_id")]
    [BsonIgnoreIfNull]
    public string? SessionId { get; set; }

    [BsonElement("user_id")]
    [BsonIgnoreIfNull]
    public string? UserId { get; set; }

    [BsonElement("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    [BsonElement("arguments")]
    public BsonDocument Arguments { get; set; } = new();

    [BsonElement("intent_id")]
    [BsonIgnoreIfNull]
    public string? IntentId { get; set; }

    [BsonElement("mode_hint")]
    [BsonIgnoreIfNull]
    public string? ModeHint { get; set; }

    [BsonElement("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [BsonElement("error_code")]
    [BsonIgnoreIfNull]
    public string? ErrorCode { get; set; }

    [BsonElement("error_message")]
    [BsonIgnoreIfNull]
    public string? ErrorMessage { get; set; }

    [BsonElement("exception_type")]
    [BsonIgnoreIfNull]
    public string? ExceptionType { get; set; }

    [BsonElement("result_summary")]
    [BsonIgnoreIfNull]
    public BsonDocument? ResultSummary { get; set; }

    [BsonElement("duration_ms")]
    public int DurationMs { get; set; }

    [BsonElement("server_version")]
    public string ServerVersion { get; set; } = string.Empty;
}
