using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class DreamRunDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("token_count")]
    public int TokenCount { get; set; }

    [BsonElement("intent_refs")]
    public List<IntentRefDocument> IntentRefs { get; set; } = [];

    [BsonElement("proposals")]
    public List<DreamProposalDocument> Proposals { get; set; } = [];

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("closed_at")]
    [BsonIgnoreIfNull]
    public DateTime? ClosedAt { get; set; }

    [BsonElement("evidence_processed")]
    public bool EvidenceProcessed { get; set; }
}

internal sealed class IntentRefDocument
{
    [BsonElement("intent_id")] public string IntentId { get; set; } = string.Empty;
    [BsonElement("token_count")] public int TokenCount { get; set; }
    [BsonElement("snapshotted_at")] public DateTime SnapshottedAt { get; set; }
}

internal sealed class DreamProposalDocument
{
    [BsonElement("id")] public string Id { get; set; } = string.Empty;
    [BsonElement("target_instruction_id")] public string TargetInstructionId { get; set; } = string.Empty;
    [BsonElement("target_kind")] public string TargetKind { get; set; } = string.Empty;
    [BsonElement("base_instruction_version")] public int BaseInstructionVersion { get; set; }
    [BsonElement("proposed_rule")] public string ProposedRule { get; set; } = string.Empty;
    [BsonElement("evidence_summary")] public string EvidenceSummary { get; set; } = string.Empty;
    [BsonElement("intent_refs")] public List<IntentRefDocument> IntentRefs { get; set; } = [];
    [BsonElement("rationale")] public string Rationale { get; set; } = string.Empty;
    [BsonElement("severity")] public string Severity { get; set; } = string.Empty;
    [BsonElement("decision")] public string Decision { get; set; } = string.Empty;

    [BsonElement("final_rule")]
    [BsonIgnoreIfNull]
    public string? FinalRule { get; set; }

    [BsonElement("applied_instruction_version")]
    [BsonIgnoreIfNull]
    public int? AppliedInstructionVersion { get; set; }

    [BsonElement("rejected_reason")]
    [BsonIgnoreIfNull]
    public string? RejectedReason { get; set; }
}
