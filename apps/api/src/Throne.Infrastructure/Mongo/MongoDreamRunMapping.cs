using Throne.Domain.DreamRuns;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo;

internal static class MongoDreamRunMapping
{
    public static DreamRunDocument ToDocument(DreamRun run) => new()
    {
        Id = run.Id.Value,
        Status = run.Status,
        WindowStart = run.WindowStart.UtcDateTime,
        WindowEnd = run.WindowEnd.UtcDateTime,
        ReadinessScore = run.ReadinessScore,
        EvidenceCounts = ToCountsDoc(run.EvidenceCounts),
        EvidenceRefs = run.EvidenceRefs.Select(ToRefDoc).ToList(),
        OmittedCounts = ToOmittedDoc(run.OmittedEvidenceCounts),
        Proposals = run.Proposals.Select(ToProposalDoc).ToList(),
        CreatedAt = run.CreatedAt.UtcDateTime,
        ClosedAt = run.ClosedAt?.UtcDateTime,
        EvidenceProcessed = run.EvidenceProcessed,
    };

    public static DreamRun ToDomain(DreamRunDocument doc) => DreamRun.Restore(
        new DreamRunId(doc.Id),
        doc.Status,
        DateTime.SpecifyKind(doc.WindowStart, DateTimeKind.Utc),
        DateTime.SpecifyKind(doc.WindowEnd, DateTimeKind.Utc),
        doc.ReadinessScore,
        ToDomainCounts(doc.EvidenceCounts),
        doc.EvidenceRefs.Select(ToDomainRef).ToList(),
        ToDomainOmitted(doc.OmittedCounts),
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
        EvidenceRefs = p.EvidenceRefs.Select(ToRefDoc).ToList(),
        Rationale = p.Rationale,
        Severity = p.Severity,
        Decision = p.Decision,
        FinalRule = p.FinalRule,
        AppliedInstructionVersion = p.AppliedInstructionVersion,
        RejectedReason = p.RejectedReason,
    };

    public static EvidenceRefDocument ToRefDoc(EvidenceRef r) => new()
    {
        Kind = r.Kind,
        Id = r.Id,
        CreatedAt = r.CreatedAt?.UtcDateTime,
    };

    private static EvidenceCountsDocument ToCountsDoc(EvidenceCounts c) => new()
    {
        Reviews = c.Reviews,
        Qa = c.Qa,
        McpErrors = c.McpErrors,
        AcceptedOutcomes = c.AcceptedOutcomes,
        ManualCorrections = c.ManualCorrections,
        VerificationFailures = c.VerificationFailures,
        SkippedProposals = c.SkippedProposals,
    };

    private static OmittedEvidenceCountsDocument ToOmittedDoc(OmittedEvidenceCounts c) => new()
    {
        TooRecent = c.TooRecent,
        BudgetExceeded = c.BudgetExceeded,
        LowPriority = c.LowPriority,
    };

    private static EvidenceCounts ToDomainCounts(EvidenceCountsDocument d) => new(
        d.Reviews, d.Qa, d.McpErrors, d.AcceptedOutcomes,
        d.ManualCorrections, d.VerificationFailures, d.SkippedProposals);

    private static OmittedEvidenceCounts ToDomainOmitted(OmittedEvidenceCountsDocument d) => new(
        d.TooRecent, d.BudgetExceeded, d.LowPriority);

    private static EvidenceRef ToDomainRef(EvidenceRefDocument d) =>
        new(d.Kind, d.Id, d.CreatedAt is null
            ? null
            : DateTime.SpecifyKind(d.CreatedAt.Value, DateTimeKind.Utc));

    private static DreamProposal ToDomainProposal(DreamProposalDocument d) => DreamProposal.Restore(
        new DreamProposalId(d.Id),
        d.TargetInstructionId,
        d.TargetKind,
        d.BaseInstructionVersion,
        d.ProposedRule,
        d.EvidenceSummary,
        d.EvidenceRefs.Select(ToDomainRef).ToList(),
        d.Rationale,
        d.Severity,
        d.Decision,
        d.FinalRule,
        d.AppliedInstructionVersion,
        d.RejectedReason);
}
