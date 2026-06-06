using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class IntentPinDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("context_tag_id")]
    public string ContextTagId { get; set; } = string.Empty;

    [BsonElement("pin_sort_key")]
    public string PinSortKey { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
