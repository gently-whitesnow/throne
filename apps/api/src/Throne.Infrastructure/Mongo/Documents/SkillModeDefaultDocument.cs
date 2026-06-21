using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class SkillModeDefaultDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("skill_id")]
    public string SkillId { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }
}
