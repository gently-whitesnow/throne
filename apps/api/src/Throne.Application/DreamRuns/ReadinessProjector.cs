namespace Throne.Application.DreamRuns;

/// <summary>
/// Pure projection of an assembled <see cref="DreamWindowAssembly"/> + pending state into
/// a <see cref="ReadinessSnapshot"/>. Three-state status: empty / has_content / pending_review.
/// No thresholds, no weights, no time window — token count is informational only (ADR-0011 v3).
/// </summary>
public static class ReadinessProjector
{
    public static ReadinessSnapshot Project(
        DreamWindowAssembly assembly,
        int pendingProposalsCount,
        int pendingRunsCount)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var status = ResolveStatus(assembly.Available.Items.Count, pendingRunsCount);
        var suggested = ResolveSuggestedAction(status);

        return new ReadinessSnapshot(
            status,
            assembly.AvailableTokens,
            assembly.LockedTokens,
            assembly.Available.Items.Count,
            pendingProposalsCount,
            pendingRunsCount,
            suggested);
    }

    private static string ResolveStatus(int intentCount, int pendingRunsCount)
    {
        if (pendingRunsCount > 0)
        {
            return ReadinessStatusNames.PendingReview;
        }
        return intentCount == 0 ? ReadinessStatusNames.Empty : ReadinessStatusNames.HasContent;
    }

    private static string ResolveSuggestedAction(string status) => status switch
    {
        ReadinessStatusNames.Empty => ReadinessSuggestedActions.Wait,
        ReadinessStatusNames.HasContent => ReadinessSuggestedActions.Run,
        ReadinessStatusNames.PendingReview => ReadinessSuggestedActions.Review,
        _ => ReadinessSuggestedActions.Wait,
    };
}
