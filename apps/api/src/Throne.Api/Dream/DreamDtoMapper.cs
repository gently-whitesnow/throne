using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;
using Throne.Dream.Contracts.Generated;

namespace Throne.Api.Dream;

internal static class DreamDtoMapper
{
    public static DreamRunDto ToRunDto(DreamRun run) => new()
    {
        Id = run.Id.Value,
        Status = ToWireStatus(run.Status),
        Window_start = run.WindowStart,
        Window_end = run.WindowEnd,
        Readiness_score = run.ReadinessScore,
        Evidence_counts = ToCountsDto(run.EvidenceCounts),
        Omitted_counts = ToOmittedDto(run.OmittedEvidenceCounts),
        Evidence_refs = run.EvidenceRefs.Select(ToRefDto).ToList(),
        Proposals = run.Proposals.Select(ToProposalDto).ToList(),
        Created_at = run.CreatedAt,
        Closed_at = run.ClosedAt ?? default,
        Evidence_processed = run.EvidenceProcessed,
    };

    public static DreamRunDetailDto ToDetailDto(GetDreamRunResult result) => new()
    {
        Run = ToRunDto(result.Run),
        Previews = result.Previews.Select(ToPreviewDto).ToList(),
    };

    public static DreamReadinessDto ToReadinessDto(ReadinessSnapshot snapshot) => new()
    {
        Status = ToReadinessStatus(snapshot.Status),
        Available_score = snapshot.AvailableScore,
        Locked_score = snapshot.LockedScore,
        Threshold = snapshot.Threshold,
        Evidence_counts = ToCountsDto(snapshot.EvidenceCounts),
        Oldest_unprocessed_at = snapshot.OldestUnprocessedAt ?? default,
        Newest_safe_evidence_at = snapshot.NewestSafeEvidenceAt ?? default,
        Safe_window_start = snapshot.SafeWindowStart,
        Safe_window_end = snapshot.SafeWindowEnd,
        Pending_proposals_count = snapshot.PendingProposalsCount,
        Pending_runs_count = snapshot.PendingRunsCount,
        Suggested_action = snapshot.SuggestedAction,
    };

    private static DreamProposalDto ToProposalDto(DreamProposal p) => new()
    {
        Id = p.Id.Value,
        Target_instruction_id = p.TargetInstructionId,
        Target_kind = p.TargetKind,
        Base_instruction_version = p.BaseInstructionVersion,
        Proposed_rule = p.ProposedRule,
        Evidence_summary = p.EvidenceSummary,
        Evidence_refs = p.EvidenceRefs.Select(ToRefDto).ToList(),
        Rationale = p.Rationale,
        Severity = ToSeverity(p.Severity),
        Decision = ToDecision(p.Decision),
        Final_rule = p.FinalRule,
        Applied_instruction_version = p.AppliedInstructionVersion ?? 0,
        Rejected_reason = p.RejectedReason,
    };

    private static DreamProposalPreviewDto ToPreviewDto(DreamProposalPreview p) => new()
    {
        Proposal_id = p.ProposalId,
        Current_text = p.CurrentText,
        Proposed_text = p.ProposedText,
        Current_instruction_version = p.CurrentInstructionVersion,
        Base_version_matches_current = p.BaseVersionMatchesCurrent,
    };

    private static DreamEvidenceCountsDto ToCountsDto(EvidenceCounts c) => new()
    {
        Reviews = c.Reviews,
        Qa = c.Qa,
        Mcp_errors = c.McpErrors,
        Accepted_outcomes = c.AcceptedOutcomes,
        Manual_corrections = c.ManualCorrections,
        Verification_failures = c.VerificationFailures,
        Skipped_proposals = c.SkippedProposals,
    };

    private static DreamOmittedCountsDto ToOmittedDto(OmittedEvidenceCounts c) => new()
    {
        Too_recent = c.TooRecent,
        Budget_exceeded = c.BudgetExceeded,
        Low_priority = c.LowPriority,
    };

    private static DreamEvidenceRefDto ToRefDto(EvidenceRef r) => new()
    {
        Kind = ToEvidenceKind(r.Kind),
        Id = r.Id,
        Created_at = r.CreatedAt ?? default,
    };

    private static DreamRunDtoStatus ToWireStatus(string status) => status switch
    {
        DreamRunStatusNames.Pending => DreamRunDtoStatus.Pending,
        DreamRunStatusNames.Closed => DreamRunDtoStatus.Closed,
        _ => throw new InvalidOperationException($"Unknown DreamRun status: {status}"),
    };

    private static DreamProposalDtoSeverity ToSeverity(string severity) => severity switch
    {
        DreamProposalSeverityNames.High => DreamProposalDtoSeverity.High,
        DreamProposalSeverityNames.Medium => DreamProposalDtoSeverity.Medium,
        DreamProposalSeverityNames.Low => DreamProposalDtoSeverity.Low,
        _ => throw new InvalidOperationException($"Unknown severity: {severity}"),
    };

    private static DreamProposalDtoDecision ToDecision(string decision) => decision switch
    {
        DreamProposalDecisionNames.Pending => DreamProposalDtoDecision.Pending,
        DreamProposalDecisionNames.Applied => DreamProposalDtoDecision.Applied,
        DreamProposalDecisionNames.Skipped => DreamProposalDtoDecision.Skipped,
        _ => throw new InvalidOperationException($"Unknown decision: {decision}"),
    };

    private static DreamEvidenceRefDtoKind ToEvidenceKind(string kind) => kind switch
    {
        EvidenceKindNames.Review => DreamEvidenceRefDtoKind.Review,
        EvidenceKindNames.Qa => DreamEvidenceRefDtoKind.Qa,
        EvidenceKindNames.McpCall => DreamEvidenceRefDtoKind.Mcp_call,
        EvidenceKindNames.Outcome => DreamEvidenceRefDtoKind.Outcome,
        EvidenceKindNames.Verification => DreamEvidenceRefDtoKind.Verification,
        EvidenceKindNames.ManualCorrection => DreamEvidenceRefDtoKind.Manual_correction,
        _ => throw new InvalidOperationException($"Unknown evidence kind: {kind}"),
    };

    private static DreamReadinessStatus ToReadinessStatus(string status) => status switch
    {
        ReadinessStatusNames.Empty => DreamReadinessStatus.Empty,
        ReadinessStatusNames.WarmingUp => DreamReadinessStatus.Warming_up,
        ReadinessStatusNames.Ready => DreamReadinessStatus.Ready,
        ReadinessStatusNames.Rich => DreamReadinessStatus.Rich,
        ReadinessStatusNames.PendingReview => DreamReadinessStatus.Pending_review,
        _ => throw new InvalidOperationException($"Unknown readiness status: {status}"),
    };
}
