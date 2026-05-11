using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class IntentLinkDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("from_id")]
    public string FromId { get; set; } = string.Empty;

    [BsonElement("to_id")]
    public string ToId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("author")]
    public string Author { get; set; } = string.Empty;

    [BsonElement("rationale")]
    public string? Rationale { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
