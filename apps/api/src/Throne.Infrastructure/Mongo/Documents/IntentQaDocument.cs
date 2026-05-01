using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class IntentQaDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("intent_version_at_write")]
    public int IntentVersionAtWrite { get; set; }

    [BsonElement("question")]
    public string Question { get; set; } = string.Empty;

    [BsonElement("answer")]
    public string Answer { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("created_by")]
    public string CreatedBy { get; set; } = string.Empty;
}
