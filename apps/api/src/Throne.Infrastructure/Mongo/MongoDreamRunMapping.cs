using Throne.Domain.DreamRuns;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal static class MongoDreamRunMapping
{
    public static DreamRunDocument ToDocument(DreamRun run) => new()
    {
        Id = run.Id.Value,
        Status = run.Status,
        TokenCount = run.TokenCount,
        IntentRefs = run.IntentRefs.Select(ToRefDoc).ToList(),
        Proposals = run.Proposals.Select(ToProposalDoc).ToList(),
        CreatedAt = run.CreatedAt.UtcDateTime,
        ClosedAt = run.ClosedAt?.UtcDateTime,
        EvidenceProcessed = run.EvidenceProcessed,
    };

    public static DreamRun ToDomain(DreamRunDocument doc) => DreamRun.Restore(
        new DreamRunId(doc.Id),
        doc.Status,
        doc.TokenCount,
        doc.IntentRefs.Select(ToDomainRef).ToList(),
        doc.Proposals.Select(ToDomainProposal).ToList(),
        DateTime.SpecifyKind(doc.CreatedAt, DateTimeKind.Utc),
        doc.ClosedAt is null ? null : DateTime.SpecifyKind(doc.ClosedAt.Value, DateTimeKind.Utc),
        doc.EvidenceProcessed);

    public static DreamProposalDocument ToProposalDoc(DreamProposal p) => new()
    {
        Id = p.Id.Value,
        TargetInstructionId = p.TargetInstructionId,
        TargetKind = p.TargetKind,
        BaseInstructionVersion = p.BaseInstructionVersion,
        ProposedRule = p.ProposedRule,
        EvidenceSummary = p.EvidenceSummary,
        IntentRefs = p.IntentRefs.Select(ToRefDoc).ToList(),
        Rationale = p.Rationale,
        Severity = p.Severity,
        Decision = p.Decision,
        FinalRule = p.FinalRule,
        AppliedInstructionVersion = p.AppliedInstructionVersion,
        RejectedReason = p.RejectedReason,
    };

    public static IntentRefDocument ToRefDoc(IntentRef r) => new()
    {
        IntentId = r.IntentId,
        TokenCount = r.TokenCount,
        SnapshottedAt = r.SnapshottedAt.UtcDateTime,
    };

    private static IntentRef ToDomainRef(IntentRefDocument d) => new(
        d.IntentId,
        d.TokenCount,
        DateTime.SpecifyKind(d.SnapshottedAt, DateTimeKind.Utc));

    private static DreamProposal ToDomainProposal(DreamProposalDocument d) => DreamProposal.Restore(
        new DreamProposalId(d.Id),
        d.TargetInstructionId,
        d.TargetKind,
        d.BaseInstructionVersion,
        d.ProposedRule,
        d.EvidenceSummary,
        d.IntentRefs.Select(ToDomainRef).ToList(),
        d.Rationale,
        d.Severity,
        d.Decision,
        d.FinalRule,
        d.AppliedInstructionVersion,
        d.RejectedReason);
}
