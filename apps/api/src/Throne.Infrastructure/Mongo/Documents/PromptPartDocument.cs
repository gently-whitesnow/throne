using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class PromptPartDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("scope")]
    public string Scope { get; set; } = string.Empty;

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("current_version")]
    public int CurrentVersion { get; set; }

    [BsonElement("mode_roles")]
    public List<PromptPartModeRoleDocument> ModeRoles { get; set; } = [];

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class PromptPartModeRoleDocument
{
    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = string.Empty;

    [BsonElement("order")]
    public int Order { get; set; }
}
