using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Result of a readiness query: total available «fuel», what's locked behind a
/// pending DreamRun, and the human-actionable status served to UI / MCP.
/// </summary>
public sealed record ReadinessSnapshot(
    string Status,
    int AvailableScore,
    int LockedScore,
    int Threshold,
    EvidenceCounts EvidenceCounts,
    DateTimeOffset? OldestUnprocessedAt,
    DateTimeOffset? NewestSafeEvidenceAt,
    DateTimeOffset SafeWindowStart,
    DateTimeOffset SafeWindowEnd,
    int PendingProposalsCount,
    int PendingRunsCount,
    string SuggestedAction);

public static class ReadinessStatusNames
{
    public const string Empty = "empty";
    public const string WarmingUp = "warming_up";
    public const string Ready = "ready";
    public const string Rich = "rich";
    public const string PendingReview = "pending_review";

    public static readonly IReadOnlyList<string> All =
    [
        Empty,
        WarmingUp,
        Ready,
        Rich,
        PendingReview,
    ];
}

public static class ReadinessSuggestedActions
{
    public const string Wait = "Wait for more signals";
    public const string Run = "Run /tdream";
    public const string Review = "Review pending dream proposals";
}
