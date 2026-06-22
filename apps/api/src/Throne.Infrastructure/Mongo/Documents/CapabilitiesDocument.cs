using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Singleton document in the <see cref="MongoCollectionNames.Settings"/> collection
/// (<c>_id = "singleton"</c>). Holds operator-selected providers per carrier
/// capability. Probe / detection results live in an in-memory TTL cache and are
/// never persisted.
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
    /// Capability key → selected provider key (e.g. <c>open_in_ide → vscode</c>).
    /// Absence means «no provider selected»; the service then falls back to the
    /// uniquely-detected provider at runtime.
    /// </summary>
    [BsonElement("selections")]
    public Dictionary<string, string> Selections { get; set; } = [];
}
