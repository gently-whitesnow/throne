using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Append-only entry in <c>intent_events</c> (ADR-0019). One document per event;
/// link events carry <c>peer_intent_id</c> so the unified timeline can match both
/// endpoints with a single OR-filter.
/// </summary>
internal sealed class IntentEventDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("intent_id")]
    public string IntentId { get; set; } = string.Empty;

    [BsonElement("peer_intent_id")]
    [BsonIgnoreIfNull]
    public string? PeerIntentId { get; set; }

    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("version")]
    [BsonIgnoreIfNull]
    public int? Version { get; set; }

    [BsonElement("text_change")]
    [BsonIgnoreIfNull]
    public IntentEventTextChangeSubdocument? TextChange { get; set; }

    [BsonElement("link")]
    [BsonIgnoreIfNull]
    public IntentEventLinkSubdocument? Link { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("created_by")]
    [BsonIgnoreIfNull]
    public string? CreatedBy { get; set; }
}

internal sealed class IntentEventTextChangeSubdocument
{
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
}

internal sealed class IntentEventLinkSubdocument
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("from_id")]
    public string FromId { get; set; } = string.Empty;

    [BsonElement("to_id")]
    public string ToId { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("author")]
    public string Author { get; set; } = string.Empty;

    [BsonElement("rationale")]
    [BsonIgnoreIfNull]
    public string? Rationale { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
