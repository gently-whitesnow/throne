using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Singleton document in the <see cref="MongoCollectionNames.Settings"/> collection
/// (<c>_id = "singleton"</c>). Holds operator-controlled capability toggles only —
/// detection results live in an in-memory TTL cache in Application and are not
/// persisted.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class CapabilitiesDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("current_version")]
    public int CurrentVersion { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Capability key → <c>enabled</c>. Absence of a key means «never persisted»
    /// (defaults to off in the Application layer).
    /// </summary>
    [BsonElement("toggles")]
    public Dictionary<string, bool> Toggles { get; set; } = [];
}
