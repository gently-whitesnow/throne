using Throne.Domain.DreamRuns;

namespace Throne.Application.DreamRuns;

/// <summary>
/// Pure function over an <see cref="EvidenceWindow"/> + pending state, returning a
/// <see cref="ReadinessSnapshot"/>. Weights / thresholds come from <see cref="DreamOptions"/>
/// so values can be tuned without code changes.
/// </summary>
public sealed class ReadinessCalculator(DreamOptions options)
{
    private readonly DreamOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public ReadinessSnapshot Calculate(
        EvidenceWindow window,
        int pendingProposalsCount,
        int pendingRunsCount,
        int lockedScore)
    {
        ArgumentNullException.ThrowIfNull(window);

        var counts = CountByKind(window.Items);
        var availableScore = ScoreFor(window.Items);
        var oldest = window.Items.Count == 0 ? (DateTimeOffset?)null : window.Items.Min(i => i.CreatedAt);
        var newest = window.Items.Count == 0 ? (DateTimeOffset?)null : window.Items.Max(i => i.CreatedAt);

        var hasHighSeverity = window.Items.Any(i => i.HighSeverity);
        var status = ResolveStatus(window.Items.Count, availableScore, hasHighSeverity, pendingRunsCount);
        var suggested = ResolveSuggestedAction(status);

        return new ReadinessSnapshot(
            status,
            availableScore,
            lockedScore,
            _options.Thresholds.Ready,
            counts,
            oldest,
            newest,
            window.WindowStart,
            window.WindowEnd,
            pendingProposalsCount,
            pendingRunsCount,
            suggested);
    }

    public int ScoreFor(IReadOnlyList<EvidenceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var w = _options.Weights;
        var total = 0;
        foreach (var item in items)
        {
            total += item.Kind switch
            {
                EvidenceKindNames.Review => item.HighSeverity ? w.ReviewSeverityHigh : w.Review,
                EvidenceKindNames.Qa => w.Qa,
                EvidenceKindNames.McpCall => w.McpCallError,
                EvidenceKindNames.Outcome => w.AcceptedOutcome,
                EvidenceKindNames.Verification => w.VerificationFailure,
                EvidenceKindNames.ManualCorrection => w.ManualCorrection,
                _ => 0,
            };
        }
        return total;
    }

    public static EvidenceCounts CountByKind(IReadOnlyList<EvidenceItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var reviews = 0;
        var qa = 0;
        var mcpErrors = 0;
        var accepted = 0;
        var manual = 0;
        var verifications = 0;
        foreach (var item in items)
        {
            switch (item.Kind)
            {
                case EvidenceKindNames.Review: reviews++; break;
                case EvidenceKindNames.Qa: qa++; break;
                case EvidenceKindNames.McpCall: mcpErrors++; break;
                case EvidenceKindNames.Outcome: accepted++; break;
                case EvidenceKindNames.ManualCorrection: manual++; break;
                case EvidenceKindNames.Verification: verifications++; break;
            }
        }
        return new EvidenceCounts(reviews, qa, mcpErrors, accepted, manual, verifications, SkippedProposals: 0);
    }

    private string ResolveStatus(int itemCount, int availableScore, bool hasHighSeverity, int pendingRunsCount)
    {
        if (pendingRunsCount > 0)
        {
            return ReadinessStatusNames.PendingReview;
        }
        if (itemCount == 0)
        {
            return ReadinessStatusNames.Empty;
        }
        if (hasHighSeverity)
        {
            return availableScore >= _options.Thresholds.Rich
                ? ReadinessStatusNames.Rich
                : ReadinessStatusNames.Ready;
        }
        if (availableScore >= _options.Thresholds.Rich)
        {
            return ReadinessStatusNames.Rich;
        }
        if (availableScore >= _options.Thresholds.Ready)
        {
            return ReadinessStatusNames.Ready;
        }
        return ReadinessStatusNames.WarmingUp;
    }

    private static string ResolveSuggestedAction(string status) => status switch
    {
        ReadinessStatusNames.Empty => ReadinessSuggestedActions.Wait,
        ReadinessStatusNames.WarmingUp => ReadinessSuggestedActions.Wait,
        ReadinessStatusNames.Ready => ReadinessSuggestedActions.Run,
        ReadinessStatusNames.Rich => ReadinessSuggestedActions.Run,
        ReadinessStatusNames.PendingReview => ReadinessSuggestedActions.Review,
        _ => ReadinessSuggestedActions.Wait,
    };
}
