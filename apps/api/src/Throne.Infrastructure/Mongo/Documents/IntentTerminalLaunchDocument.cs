using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Per-intent persisted launch axis (ADR-0041) in the
/// <see cref="MongoCollectionNames.TerminalLaunches"/> collection. <c>_id</c> is the intent id —
/// one record per intent, upserted on each successful spawn. Liveness is never stored here; it
/// stays <c>tmux has-session</c>-derived. Values are wire strings (snake_case), no enum codes.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class IntentTerminalLaunchDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("mode")]
    public string Mode { get; set; } = string.Empty;

    [BsonElement("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [BsonElement("model")]
    public string Model { get; set; } = string.Empty;

    [BsonElement("effort")]
    [BsonIgnoreIfNull]
    public string? Effort { get; set; }
}
