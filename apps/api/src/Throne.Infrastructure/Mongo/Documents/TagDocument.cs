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

    // Denormalized counter-cache: number of intents referencing this tag. Maintained
    // transactionally on the intent write-path; powers the usage-desc keyset sort and
    // the inline intents_count. Legacy docs without the field read back as 0; the
    // startup backfill seeds the real count once.
    [BsonElement("usage_count")]
    public int UsageCount { get; set; }

    [BsonElement("default_repositories")]
    public List<TagDefaultRepositoryDocument> DefaultRepositories { get; set; } = [];
}
