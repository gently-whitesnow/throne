using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class IntentStatusChangeDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("intent_version_at_write")]
    public int IntentVersionAtWrite { get; set; }

    [BsonElement("from_status")]
    public string FromStatus { get; set; } = string.Empty;

    [BsonElement("to_status")]
    public string ToStatus { get; set; } = string.Empty;

    [BsonElement("source")]
    public string Source { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
}
