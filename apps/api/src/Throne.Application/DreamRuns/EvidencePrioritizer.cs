using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Deterministic evidence ordering used by run_dream when packing context for the agent.
/// Rules (Intent 4 §c, ADR-0011 invariants):
/// 1. high-severity items first;
/// 2. <c>review</c> before <c>qa</c>;
/// 3. <c>verification</c> before <c>mcp_call</c> errors;
/// 4. <c>mcp_call</c> errors before <c>outcome</c> success records;
/// 5. recent items before old ones (so freshly closed sessions surface first).
/// </summary>
public static class EvidencePrioritizer
{
    public static IReadOnlyList<EvidenceItem> Prioritize(IReadOnlyList<EvidenceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .OrderByDescending(i => i.HighSeverity)
            .ThenBy(KindRank)
            .ThenByDescending(i => i.CreatedAt)
            .ThenBy(i => i.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static int KindRank(EvidenceItem item) => item.Kind switch
    {
        EvidenceKindNames.ManualCorrection => 0,
        EvidenceKindNames.Review => 1,
        EvidenceKindNames.Qa => 2,
        EvidenceKindNames.Verification => 3,
        EvidenceKindNames.McpCall => 4,
        EvidenceKindNames.Outcome => 5,
        _ => 99,
    };
}
