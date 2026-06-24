using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class TagDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("current_version")]
    public int CurrentVersion { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("last_attached_at")]
    [BsonIgnoreIfNull]
    public DateTime? LastAttachedAt { get; set; }

    [BsonElement("default_repositories")]
    public List<TagDefaultRepositoryDocument> DefaultRepositories { get; set; } = [];
}
