// MCP wire-format requires snake_case parameter names; tools are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;

namespace Throne.Api.Mcp.Tools;

public sealed record DreamReadinessDto(
    [property: Description("Aggregated status: 'empty', 'has_content', or 'pending_review'.")] string Status,
    [property: Description("Tokens of unique content available for the next /dream invocation.")] int AvailableTokens,
    [property: Description("Tokens belonging to intents already locked by pending DreamRuns.")] int LockedTokens,
    [property: Description("Number of distinct intents currently in the available bucket.")] int IntentCount,
    [property: Description("Informational suggestion ('Run /dream', 'Wait for more signals', 'Review pending dream proposals'). Never blocking.")] string SuggestedAction,
    [property: Description("Total proposals in pending state across all open DreamRuns.")] int PendingProposalsCount,
    [property: Description("Number of pending DreamRuns. While >0 readiness reports 'pending_review' to discourage parallel /dream.")] int PendingRunsCount);

public sealed record DreamIntentRefDto(
    [property: Description("Intent identifier captured in the snapshot.")] string IntentId,
    [property: Description("cl100k_base tokens contributed by this intent.")] int TokenCount,
    [property: Description("UTC moment the intent was snapshotted into the run.")] DateTimeOffset SnapshottedAt);

public sealed record DreamRunDto(
    [property: Description("DreamRun identifier.")] string Id,
    [property: Description("Run status: 'pending' or 'closed'.")] string Status,
    [property: Description("Sum of tokens across all snapshotted intents.")] int TokenCount,
    [property: Description("Per-intent breakdown captured in the snapshot.")] IReadOnlyList<DreamIntentRefDto> IntentRefs,
    [property: Description("Snapshot creation timestamp (UTC).")] DateTimeOffset CreatedAt,
    [property: Description("Close timestamp; null while the run is pending.")] DateTimeOffset? ClosedAt,
    [property: Description("True when this closed run consumed its intents (won't resurface in the next /dream).")] bool EvidenceProcessed,
    [property: Description("Number of proposals already attached to this run.")] int ProposalsCount);

public sealed record RunDreamResultDto(
    [property: Description("Outcome of the run_dream invocation: 'created', 'not_enough_context', or 'existing_pending'.")] string Status,
    [property: Description("Readiness snapshot at the moment the call was processed.")] DreamReadinessDto Readiness,
    [property: Description("Pending DreamRun payload if status='created' or status='existing_pending'; null otherwise.")] DreamRunPayloadDto? DreamRun,
    [property: Description("Human-readable explanation when status='not_enough_context'; null otherwise.")] string? Reason);

public sealed record DreamRunPayloadDto(
    [property: Description("DreamRun snapshot the agent should reason over.")] DreamRunDto Run,
    [property: Description("Aggregated summary served to the agent in lieu of raw documents.")] DreamEvidenceSummaryDto EvidenceSummary,
    [property: Description("Allowed intent references for follow-up propose_dream_rule calls.")] IReadOnlyList<DreamIntentRefDto> IntentRefs);

public sealed record DreamEvidenceSummaryDto(
    [property: Description("Number of distinct intents in the snapshot.")] int IntentCount,
    [property: Description("Total tokens captured in the snapshot.")] int TokenCount,
    [property: Description("Already-learned rules grouped by user instruction kind. The agent uses this to avoid duplicate proposals.")] IReadOnlyDictionary<string, IReadOnlyList<DreamLearnedRuleDto>> ExistingLearnedRulesByKind);

public sealed record DreamLearnedRuleDto(
    [property: Description("Existing rule text exactly as it appears under '## Learned rules'.")] string RuleText);

public sealed record ProposeDreamRuleResultDto(
    [property: Description("Identifier of the freshly created proposal.")] string ProposalId,
    [property: Description("Decision state of the proposal — always 'pending' on creation.")] string Status);

internal static class DreamMcpDtoMapper
{
    public static DreamReadinessDto ToReadiness(ReadinessSnapshot snapshot) => new(
        Status: snapshot.Status,
        AvailableTokens: snapshot.AvailableTokens,
        LockedTokens: snapshot.LockedTokens,
        IntentCount: snapshot.IntentCount,
        SuggestedAction: snapshot.SuggestedAction,
        PendingProposalsCount: snapshot.PendingProposalsCount,
        PendingRunsCount: snapshot.PendingRunsCount);

    public static DreamRunDto ToRun(DreamRun run) => new(
        Id: run.Id.Value,
        Status: run.Status,
        TokenCount: run.TokenCount,
        IntentRefs: run.IntentRefs.Select(ToIntentRef).ToArray(),
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
        IntentRefs: payload.IntentRefs.Select(ToIntentRef).ToArray());

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
            IntentCount: summary.IntentCount,
            TokenCount: summary.TokenCount,
            ExistingLearnedRulesByKind: rules);
    }

    private static DreamIntentRefDto ToIntentRef(IntentRef r) => new(r.IntentId, r.TokenCount, r.SnapshottedAt);
}
