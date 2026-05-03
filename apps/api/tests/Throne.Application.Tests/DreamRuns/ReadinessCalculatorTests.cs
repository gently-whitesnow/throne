using FluentAssertions;
using Throne.Application.DreamRuns;
using Throne.Domain.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class ReadinessCalculatorTests
{
    private static readonly DateTimeOffset WindowStart = new(2026, 4, 24, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Empty window → status=empty, score=0, suggested=Wait")]
    public void Empty_window_yields_empty_status()
    {
        var calc = new ReadinessCalculator(new DreamOptions());
        var snapshot = calc.Calculate(new EvidenceWindow(WindowStart, WindowEnd, []), 0, 0, lockedScore: 0);
        snapshot.Status.Should().Be(ReadinessStatusNames.Empty);
        snapshot.AvailableScore.Should().Be(0);
        snapshot.SuggestedAction.Should().Be(ReadinessSuggestedActions.Wait);
    }

    [Fact(DisplayName = "Pending review существует → status=pending_review, suggested=Review")]
    public void Pending_runs_take_priority()
    {
        var calc = new ReadinessCalculator(new DreamOptions());
        var snapshot = calc.Calculate(
            new EvidenceWindow(WindowStart, WindowEnd, [Item(EvidenceKindNames.Review)]),
            pendingProposalsCount: 1,
            pendingRunsCount: 1,
            lockedScore: 5);
        snapshot.Status.Should().Be(ReadinessStatusNames.PendingReview);
        snapshot.SuggestedAction.Should().Be(ReadinessSuggestedActions.Review);
        snapshot.LockedScore.Should().Be(5);
    }

    [Fact(DisplayName = "Один review (вес 5) с порогом 10 → warming_up")]
    public void Below_ready_threshold_warms_up()
    {
        var calc = new ReadinessCalculator(new DreamOptions());
        var snapshot = calc.Calculate(
            new EvidenceWindow(WindowStart, WindowEnd, [Item(EvidenceKindNames.Review)]),
            0, 0, 0);
        snapshot.AvailableScore.Should().Be(5);
        snapshot.Status.Should().Be(ReadinessStatusNames.WarmingUp);
    }

    [Fact(DisplayName = "Два review (5+5=10) → ready, suggested=Run")]
    public void At_ready_threshold_returns_ready()
    {
        var calc = new ReadinessCalculator(new DreamOptions());
        var snapshot = calc.Calculate(
            new EvidenceWindow(WindowStart, WindowEnd,
                [Item(EvidenceKindNames.Review), Item(EvidenceKindNames.Review)]),
            0, 0, 0);
        snapshot.AvailableScore.Should().Be(10);
        snapshot.Status.Should().Be(ReadinessStatusNames.Ready);
        snapshot.SuggestedAction.Should().Be(ReadinessSuggestedActions.Run);
    }

    [Fact(DisplayName = "High-severity review даже при недостаточном score возвращает ready")]
    public void High_severity_force_ready()
    {
        var calc = new ReadinessCalculator(new DreamOptions());
        var snapshot = calc.Calculate(
            new EvidenceWindow(WindowStart, WindowEnd,
                [Item(EvidenceKindNames.Review, highSeverity: true)]),
            0, 0, 0);
        snapshot.AvailableScore.Should().Be(10);
        snapshot.Status.Should().Be(ReadinessStatusNames.Ready);
    }

    [Fact(DisplayName = "Веса учитываются: manual_correction=8 + qa=1 = 9 → warming_up")]
    public void Weights_per_kind()
    {
        var calc = new ReadinessCalculator(new DreamOptions());
        var snapshot = calc.Calculate(
            new EvidenceWindow(WindowStart, WindowEnd,
                [Item(EvidenceKindNames.ManualCorrection), Item(EvidenceKindNames.Qa)]),
            0, 0, 0);
        snapshot.AvailableScore.Should().Be(9);
        snapshot.EvidenceCounts.ManualCorrections.Should().Be(1);
        snapshot.EvidenceCounts.Qa.Should().Be(1);
    }

    [Fact(DisplayName = "Кастомный порог из DreamOptions меняет точку перехода в ready")]
    public void Custom_threshold_changes_status()
    {
        var options = new DreamOptions { Thresholds = new DreamReadinessThresholds { Ready = 25, Rich = 60 } };
        var calc = new ReadinessCalculator(options);
        var snapshot = calc.Calculate(
            new EvidenceWindow(WindowStart, WindowEnd,
                [Item(EvidenceKindNames.Review), Item(EvidenceKindNames.Review)]),
            0, 0, 0);
        snapshot.AvailableScore.Should().Be(10);
        snapshot.Status.Should().Be(ReadinessStatusNames.WarmingUp);
        snapshot.Threshold.Should().Be(25);
    }

    private static EvidenceItem Item(string kind, bool highSeverity = false) =>
        new(kind, Guid.NewGuid().ToString("N"), WindowEnd.AddHours(-1), SessionId: null, highSeverity);
}
