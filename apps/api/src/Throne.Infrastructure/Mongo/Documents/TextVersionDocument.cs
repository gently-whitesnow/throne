using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class TextVersionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("owner_kind")]
    public string OwnerKind { get; set; } = string.Empty;

    [BsonElement("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("snapshot")]
    [BsonIgnoreIfNull]
    public string? Snapshot { get; set; }

    [BsonElement("old_text")]
    [BsonIgnoreIfNull]
    public string? OldText { get; set; }

    [BsonElement("new_text")]
    [BsonIgnoreIfNull]
    public string? NewText { get; set; }

    [BsonElement("after_line")]
    [BsonIgnoreIfNull]
    public int? AfterLine { get; set; }

    [BsonElement("insert_text")]
    [BsonIgnoreIfNull]
    public string? InsertText { get; set; }

    [BsonElement("changed_at")]
    public DateTime ChangedAt { get; set; }

    [BsonElement("changed_by")]
    public string ChangedBy { get; set; } = string.Empty;
}
