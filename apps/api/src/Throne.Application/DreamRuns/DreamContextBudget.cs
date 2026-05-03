using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Server-side cap on how much evidence can land in a DreamRun snapshot.
/// Hidden from the agent surface: the agent never sees per-kind limits or
/// total budget, only the assembled summary and the trimmed
/// <c>evidence_refs</c>. See Intent 4 §d.
/// </summary>
public sealed record DreamContextBudget(
    int MaxReviews,
    int MaxQa,
    int MaxMcpCalls,
    int MaxOutcomes,
    int MaxVerifications,
    int MaxManualCorrections,
    int MaxPatterns)
{
    public static readonly DreamContextBudget Default = new(
        MaxReviews: 200,
        MaxQa: 200,
        MaxMcpCalls: 500,
        MaxOutcomes: 200,
        MaxVerifications: 200,
        MaxManualCorrections: 200,
        MaxPatterns: 10);

    public int CapFor(string kind) => kind switch
    {
        EvidenceKindNames.Review => MaxReviews,
        EvidenceKindNames.Qa => MaxQa,
        EvidenceKindNames.McpCall => MaxMcpCalls,
        EvidenceKindNames.Outcome => MaxOutcomes,
        EvidenceKindNames.Verification => MaxVerifications,
        EvidenceKindNames.ManualCorrection => MaxManualCorrections,
        _ => 0,
    };
}

/// <summary>
/// Result of applying <see cref="DreamContextBudget"/> to prioritized evidence:
/// the surviving refs (in priority order) and per-bucket counts of items the
/// budget shaved off.
/// </summary>
public sealed record DreamContextPack(
    IReadOnlyList<EvidenceRef> EvidenceRefs,
    EvidenceCounts Counts,
    OmittedEvidenceCounts Omitted);

public static class DreamContextBudgetApplier
{
    public static DreamContextPack Apply(
        IReadOnlyList<EvidenceItem> prioritized,
        DreamContextBudget budget)
    {
        ArgumentNullException.ThrowIfNull(prioritized);
        ArgumentNullException.ThrowIfNull(budget);

        var perKind = new Dictionary<string, int>(StringComparer.Ordinal);
        var refs = new List<EvidenceRef>(prioritized.Count);
        var reviews = 0;
        var qa = 0;
        var mcp = 0;
        var outcomes = 0;
        var verifications = 0;
        var manual = 0;
        var omitted = 0;

        foreach (var item in prioritized)
        {
            var cap = budget.CapFor(item.Kind);
            if (cap <= 0)
            {
                continue;
            }
            perKind.TryGetValue(item.Kind, out var taken);
            if (taken >= cap)
            {
                omitted++;
                continue;
            }
            perKind[item.Kind] = taken + 1;
            refs.Add(item.ToRef());

            switch (item.Kind)
            {
                case EvidenceKindNames.Review: reviews++; break;
                case EvidenceKindNames.Qa: qa++; break;
                case EvidenceKindNames.McpCall: mcp++; break;
                case EvidenceKindNames.Outcome: outcomes++; break;
                case EvidenceKindNames.Verification: verifications++; break;
                case EvidenceKindNames.ManualCorrection: manual++; break;
            }
        }

        var counts = new EvidenceCounts(
            Reviews: reviews,
            Qa: qa,
            McpErrors: mcp,
            AcceptedOutcomes: outcomes,
            ManualCorrections: manual,
            VerificationFailures: verifications,
            SkippedProposals: 0);

        var omittedCounts = new OmittedEvidenceCounts(
            TooRecent: 0,
            BudgetExceeded: omitted,
            LowPriority: 0);

        return new DreamContextPack(refs, counts, omittedCounts);
    }
}
