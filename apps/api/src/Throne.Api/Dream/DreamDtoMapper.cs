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
        Token_count = run.TokenCount,
        Intent_refs = run.IntentRefs.Select(ToIntentRefDto).ToList(),
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
        Available_tokens = snapshot.AvailableTokens,
        Locked_tokens = snapshot.LockedTokens,
        Intent_count = snapshot.IntentCount,
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
        Intent_refs = p.IntentRefs.Select(ToIntentRefDto).ToList(),
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

    private static DreamIntentRefDto ToIntentRefDto(IntentRef r) => new()
    {
        Intent_id = r.IntentId,
        Token_count = r.TokenCount,
        Snapshotted_at = r.SnapshottedAt,
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

    private static DreamReadinessStatus ToReadinessStatus(string status) => status switch
    {
        ReadinessStatusNames.Empty => DreamReadinessStatus.Empty,
        ReadinessStatusNames.HasContent => DreamReadinessStatus.Has_content,
        ReadinessStatusNames.PendingReview => DreamReadinessStatus.Pending_review,
        _ => throw new InvalidOperationException($"Unknown readiness status: {status}"),
    };
}
