using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

internal sealed class InstructionPatchDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("target_kind")]
    public string TargetKind { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("patch_text")]
    public string PatchText { get; set; } = string.Empty;

    [BsonElement("applied_text")]
    [BsonIgnoreIfNull]
    public string? AppliedText { get; set; }

    [BsonElement("evidence_card_ids")]
    public List<string> EvidenceCardIds { get; set; } = new();

    [BsonElement("rationale")]
    public string Rationale { get; set; } = string.Empty;

    [BsonElement("reject_comment")]
    [BsonIgnoreIfNull]
    public string? RejectComment { get; set; }

    [BsonElement("base_instruction_version")]
    public int BaseInstructionVersion { get; set; }

    [BsonElement("applied_instruction_version")]
    [BsonIgnoreIfNull]
    public int? AppliedInstructionVersion { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("decided_at")]
    [BsonIgnoreIfNull]
    public DateTime? DecidedAt { get; set; }

    /// <summary>
    /// Optional caller-supplied retry-dedup key (≤64 chars). When set, a unique
    /// index on idempotency_key makes second-write races fail loudly so the
    /// repository can resolve the original row.
    /// </summary>
    [BsonElement("idempotency_key")]
    [BsonIgnoreIfNull]
    public string? IdempotencyKey { get; set; }
}
