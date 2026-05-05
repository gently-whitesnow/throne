using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class IntentDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("owner_user_id")]
    public string OwnerUserId { get; set; } = string.Empty;

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("current_version")]
    public int CurrentVersion { get; set; }

    [BsonElement("tag_ids")]
    public List<string> TagIds { get; set; } = [];

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
