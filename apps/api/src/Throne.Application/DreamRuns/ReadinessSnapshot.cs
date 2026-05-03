namespace Throne.Application.DreamRuns;

/// <summary>
/// Readiness snapshot for the «dream» pipeline. Token-counter model (ADR-0011 v3).
/// <see cref="Status"/> is informational and never blocks /dream — the user always
/// decides whether to run.
/// </summary>
public sealed record ReadinessSnapshot(
    string Status,
    int AvailableTokens,
    int LockedTokens,
    int IntentCount,
    int PendingProposalsCount,
    int PendingRunsCount,
    string SuggestedAction);

public static class ReadinessStatusNames
{
    public const string Empty = "empty";
    public const string HasContent = "has_content";
    public const string PendingReview = "pending_review";

    public static readonly IReadOnlyList<string> All = [Empty, HasContent, PendingReview];

    public static bool IsKnown(string value) => All.Contains(value, StringComparer.Ordinal);
}

public static class ReadinessSuggestedActions
{
    public const string Wait = "Wait for more signals";
    public const string Run = "Run /dream";
    public const string Review = "Review pending dream proposals";
}
