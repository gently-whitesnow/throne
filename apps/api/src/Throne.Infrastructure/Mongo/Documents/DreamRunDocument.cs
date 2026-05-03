using MongoDB.Bson.Serialization.Attributes;

namespace Throne.Infrastructure.Mongo.Documents;

[BsonIgnoreExtraElements]
internal sealed class DreamRunDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;

    [BsonElement("window_start")]
    public DateTime WindowStart { get; set; }

    [BsonElement("window_end")]
    public DateTime WindowEnd { get; set; }

    [BsonElement("readiness_score")]
    public int ReadinessScore { get; set; }

    [BsonElement("evidence_counts")]
    public EvidenceCountsDocument EvidenceCounts { get; set; } = new();

    [BsonElement("evidence_refs")]
    public List<EvidenceRefDocument> EvidenceRefs { get; set; } = [];

    [BsonElement("omitted_counts")]
    public OmittedEvidenceCountsDocument OmittedCounts { get; set; } = new();

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

internal sealed class EvidenceCountsDocument
{
    [BsonElement("reviews")] public int Reviews { get; set; }
    [BsonElement("qa")] public int Qa { get; set; }
    [BsonElement("mcp_errors")] public int McpErrors { get; set; }
    [BsonElement("accepted_outcomes")] public int AcceptedOutcomes { get; set; }
    [BsonElement("manual_corrections")] public int ManualCorrections { get; set; }
    [BsonElement("verification_failures")] public int VerificationFailures { get; set; }
    [BsonElement("skipped_proposals")] public int SkippedProposals { get; set; }
}

internal sealed class OmittedEvidenceCountsDocument
{
    [BsonElement("too_recent")] public int TooRecent { get; set; }
    [BsonElement("budget_exceeded")] public int BudgetExceeded { get; set; }
    [BsonElement("low_priority")] public int LowPriority { get; set; }
}

internal sealed class EvidenceRefDocument
{
    [BsonElement("kind")] public string Kind { get; set; } = string.Empty;
    [BsonElement("id")] public string Id { get; set; } = string.Empty;
    [BsonElement("created_at")]
    [BsonIgnoreIfNull]
    public DateTime? CreatedAt { get; set; }
}

internal sealed class DreamProposalDocument
{
    [BsonElement("id")] public string Id { get; set; } = string.Empty;
    [BsonElement("target_instruction_id")] public string TargetInstructionId { get; set; } = string.Empty;
    [BsonElement("target_kind")] public string TargetKind { get; set; } = string.Empty;
    [BsonElement("base_instruction_version")] public int BaseInstructionVersion { get; set; }
    [BsonElement("proposed_rule")] public string ProposedRule { get; set; } = string.Empty;
    [BsonElement("evidence_summary")] public string EvidenceSummary { get; set; } = string.Empty;
    [BsonElement("evidence_refs")] public List<EvidenceRefDocument> EvidenceRefs { get; set; } = [];
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
