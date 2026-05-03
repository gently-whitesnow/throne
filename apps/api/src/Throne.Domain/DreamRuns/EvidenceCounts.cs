namespace Throne.Domain.DreamRuns;

/// <summary>
/// Aggregated counts of raw evidence consumed by a DreamRun, per kind.
/// Used for UI breakdown and for debugging weight calculations.
/// </summary>
public sealed record EvidenceCounts(
    int Reviews,
    int Qa,
    int McpErrors,
    int AcceptedOutcomes,
    int ManualCorrections,
    int VerificationFailures,
    int SkippedProposals)
{
    public static readonly EvidenceCounts Zero = new(0, 0, 0, 0, 0, 0, 0);

    public int Total => Reviews + Qa + McpErrors + AcceptedOutcomes
        + ManualCorrections + VerificationFailures + SkippedProposals;
}

/// <summary>
/// Counts of evidence that the server skipped when assembling a DreamRun's context pack
/// (too recent, budget exceeded, low priority). Useful for «why didn't this signal show up?».
/// </summary>
public sealed record OmittedEvidenceCounts(int TooRecent, int BudgetExceeded, int LowPriority)
{
    public static readonly OmittedEvidenceCounts Zero = new(0, 0, 0);

    public int Total => TooRecent + BudgetExceeded + LowPriority;
}
