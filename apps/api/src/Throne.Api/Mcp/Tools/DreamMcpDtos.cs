// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;

namespace Throne.Api.Mcp.Tools;

public sealed record DreamReadinessDto(
    [property: Description("Aggregated status: 'empty', 'warming_up', 'ready', 'rich', or 'pending_review'.")] string Status,
    [property: Description("Sum of weighted available evidence in the safe window.")] int ReadinessScore,
    [property: Description("Score belonging to evidence already locked by pending DreamRuns.")] int LockedScore,
    [property: Description("Score required to enter 'ready' state (configurable).")] int Threshold,
    [property: Description("Per-kind counts of evidence in the safe window.")] DreamEvidenceCountsDto EvidenceCounts,
    [property: Description("Inclusive lower bound of the safe window.")] DateTimeOffset SafeWindowStart,
    [property: Description("Exclusive upper bound of the safe window.")] DateTimeOffset SafeWindowEnd,
    [property: Description("Suggested action: 'Run /tdream', 'Wait for more signals', or 'Review pending dream proposals'.")] string SuggestedAction,
    [property: Description("Total proposals in pending state across all open DreamRuns.")] int PendingProposalsCount,
    [property: Description("Number of pending DreamRuns. While >0 readiness reports 'pending_review' to discourage parallel /tdream.")] int PendingRunsCount,
    [property: Description("Created_at of the oldest unprocessed evidence record, or null when window is empty.")] DateTimeOffset? OldestUnprocessedAt,
    [property: Description("Created_at of the newest evidence record inside the safe window, or null when empty.")] DateTimeOffset? NewestSafeEvidenceAt);

public sealed record DreamEvidenceCountsDto(
    int Reviews,
    int Qa,
    int McpErrors,
    int AcceptedOutcomes,
    int ManualCorrections,
    int VerificationFailures,
    int SkippedProposals);

public sealed record DreamOmittedEvidenceCountsDto(int TooRecent, int BudgetExceeded, int LowPriority);

public sealed record DreamRunDto(
    [property: Description("DreamRun identifier.")] string Id,
    [property: Description("Run status: 'pending' or 'closed'.")] string Status,
    [property: Description("Inclusive lower bound of the run's frozen window.")] DateTimeOffset WindowStart,
    [property: Description("Exclusive upper bound of the run's frozen window.")] DateTimeOffset WindowEnd,
    [property: Description("Score captured when the snapshot was taken.")] int ReadinessScore,
    [property: Description("Per-kind evidence counts captured in the snapshot.")] DreamEvidenceCountsDto EvidenceCounts,
    [property: Description("Per-bucket counts of evidence dropped by the server's context budget.")] DreamOmittedEvidenceCountsDto OmittedEvidenceCounts,
    [property: Description("Snapshot creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Close timestamp; null while the run is pending.")] DateTimeOffset? ClosedAt,
    [property: Description("True when this closed run consumed its evidence (won't resurface in the next /tdream).")] bool EvidenceProcessed,
    [property: Description("Number of proposals already attached to this run.")] int ProposalsCount);

public sealed record RunDreamResultDto(
    [property: Description("Outcome of the run_dream invocation: 'created', 'not_enough_context', or 'existing_pending'.")] string Status,
    [property: Description("Readiness snapshot at the moment the call was processed.")] DreamReadinessDto Readiness,
    [property: Description("Pending DreamRun payload if status='created' or status='existing_pending'; null otherwise.")] DreamRunPayloadDto? DreamRun,
    [property: Description("Human-readable explanation when status='not_enough_context'; null otherwise.")] string? Reason);

public sealed record DreamRunPayloadDto(
    [property: Description("DreamRun snapshot the agent should reason over.")] DreamRunDto Run,
    [property: Description("Aggregated evidence summary served to the agent in lieu of raw documents.")] DreamEvidenceSummaryDto EvidenceSummary,
    [property: Description("Allowed evidence references for follow-up propose_dream_rule calls.")] IReadOnlyList<DreamEvidenceRefDto> EvidenceRefs);

public sealed record DreamEvidenceSummaryDto(
    [property: Description("Per-kind evidence counts captured in the snapshot.")] DreamEvidenceCountsDto Counts,
    [property: Description("Top patterns extracted from the snapshot. Capped at the server's max_patterns budget.")] IReadOnlyList<DreamEvidencePatternDto> Patterns,
    [property: Description("Suggested user instruction kinds for the agent to consider when drafting proposals.")] IReadOnlyList<string> SuggestedTargetKinds,
    [property: Description("Already-learned rules grouped by user instruction kind. The agent uses this to avoid duplicate proposals.")] IReadOnlyDictionary<string, IReadOnlyList<DreamLearnedRuleDto>> ExistingLearnedRulesByKind);

public sealed record DreamEvidencePatternDto(
    [property: Description("Evidence kind, e.g. 'review' or 'mcp_call'.")] string Kind,
    [property: Description("Number of items in the snapshot for this pattern.")] int Count,
    [property: Description("True when at least one item carried a high-severity flag.")] bool HighSeverity);

public sealed record DreamEvidenceRefDto(
    [property: Description("Evidence kind: 'review', 'qa', 'mcp_call', 'outcome', 'verification', or 'manual_correction'.")] string Kind,
    [property: Description("Evidence record id within the source collection.")] string Id,
    [property: Description("Creation timestamp (UTC) when known.")] DateTimeOffset? CreatedAt);

public sealed record DreamLearnedRuleDto(
    [property: Description("Existing rule text exactly as it appears under '## Learned rules'.")] string RuleText);

public sealed record ProposeDreamRuleResultDto(
    [property: Description("Identifier of the freshly created proposal.")] string ProposalId,
    [property: Description("Decision state of the proposal — always 'pending' on creation.")] string Status);

internal static class DreamMcpDtoMapper
{
    public static DreamReadinessDto ToReadiness(ReadinessSnapshot snapshot) => new(
        Status: snapshot.Status,
        ReadinessScore: snapshot.AvailableScore,
        LockedScore: snapshot.LockedScore,
        Threshold: snapshot.Threshold,
        EvidenceCounts: ToCounts(snapshot.EvidenceCounts),
        SafeWindowStart: snapshot.SafeWindowStart,
        SafeWindowEnd: snapshot.SafeWindowEnd,
        SuggestedAction: snapshot.SuggestedAction,
        PendingProposalsCount: snapshot.PendingProposalsCount,
        PendingRunsCount: snapshot.PendingRunsCount,
        OldestUnprocessedAt: snapshot.OldestUnprocessedAt,
        NewestSafeEvidenceAt: snapshot.NewestSafeEvidenceAt);

    public static DreamRunDto ToRun(DreamRun run) => new(
        Id: run.Id.Value,
        Status: run.Status,
        WindowStart: run.WindowStart,
        WindowEnd: run.WindowEnd,
        ReadinessScore: run.ReadinessScore,
        EvidenceCounts: ToCounts(run.EvidenceCounts),
        OmittedEvidenceCounts: ToOmitted(run.OmittedEvidenceCounts),
        CreatedAt: run.CreatedAt,
        ClosedAt: run.ClosedAt,
        EvidenceProcessed: run.EvidenceProcessed,
        ProposalsCount: run.Proposals.Count);

    public static RunDreamResultDto ToRunDreamResult(RunDreamResult result) => new(
        Status: result.Status,
        Readiness: ToReadiness(result.Readiness),
        DreamRun: result.DreamRun is null ? null : ToPayload(result.DreamRun),
        Reason: result.Reason);

    public static ProposeDreamRuleResultDto ToProposeResult(ProposeDreamRuleResult result) =>
        new(result.ProposalId, result.Status);

    private static DreamRunPayloadDto ToPayload(DreamRunPayload payload) => new(
        Run: ToRun(payload.Run),
        EvidenceSummary: ToSummary(payload.EvidenceSummary),
        EvidenceRefs: payload.EvidenceRefs.Select(ToRef).ToArray());

    private static DreamEvidenceSummaryDto ToSummary(DreamEvidenceSummary summary)
    {
        var rules = new Dictionary<string, IReadOnlyList<DreamLearnedRuleDto>>(StringComparer.Ordinal);
        foreach (var (kind, list) in summary.ExistingLearnedRulesByKind)
        {
            rules[kind] = list
                .Select(r => new DreamLearnedRuleDto(r.RuleText))
                .ToArray();
        }
        return new DreamEvidenceSummaryDto(
            Counts: ToCounts(summary.Counts),
            Patterns: summary.Patterns.Select(p => new DreamEvidencePatternDto(p.Kind, p.Count, p.HighSeverity)).ToArray(),
            SuggestedTargetKinds: summary.SuggestedTargetKinds,
            ExistingLearnedRulesByKind: rules);
    }

    private static DreamEvidenceRefDto ToRef(EvidenceRef r) => new(r.Kind, r.Id, r.CreatedAt);

    private static DreamEvidenceCountsDto ToCounts(EvidenceCounts c) => new(
        Reviews: c.Reviews,
        Qa: c.Qa,
        McpErrors: c.McpErrors,
        AcceptedOutcomes: c.AcceptedOutcomes,
        ManualCorrections: c.ManualCorrections,
        VerificationFailures: c.VerificationFailures,
        SkippedProposals: c.SkippedProposals);

    private static DreamOmittedEvidenceCountsDto ToOmitted(OmittedEvidenceCounts o) => new(
        TooRecent: o.TooRecent,
        BudgetExceeded: o.BudgetExceeded,
        LowPriority: o.LowPriority);
}
