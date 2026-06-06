using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

/// <summary>
/// Mongo persistence shape for <see cref="Throne.Domain.Dreams.DreamSession"/>.
/// </summary>
[BsonIgnoreExtraElements]
internal sealed class DreamSessionDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("vendor")]
    public string Vendor { get; set; } = string.Empty;

    /// <summary>
    /// Agent-reported source machine for this /dream pass (typically the OS
    /// hostname). Frontier-frontier scoping uses this to compute the
    /// «what's already processed» set per machine. Null for legacy records
    /// written before the field existed.
    /// </summary>
    [BsonElement("host")]
    [BsonIgnoreIfNull]
    public string? Host { get; set; }

    [BsonElement("date_from")]
    [BsonIgnoreIfNull]
    public DateTime? DateFrom { get; set; }

    [BsonElement("date_to")]
    [BsonIgnoreIfNull]
    public DateTime? DateTo { get; set; }

    [BsonElement("processed_conversation_ids")]
    public List<string> ProcessedConversationIds { get; set; } = new();

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("reflection")]
    [BsonIgnoreIfNull]
    public string? Reflection { get; set; }

    [BsonElement("proposed_patch_ids")]
    public List<string> ProposedPatchIds { get; set; } = new();
}
