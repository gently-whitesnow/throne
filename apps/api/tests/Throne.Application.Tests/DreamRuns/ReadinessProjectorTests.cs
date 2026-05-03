using FluentAssertions;
using Throne.Application.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class ReadinessProjectorTests
{
    private static readonly DateTimeOffset WindowStart = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = WindowStart.AddDays(1);

    [Fact(DisplayName = "Пустое окно без pending → status=empty, action=Wait")]
    public void Empty_window_returns_empty_status()
    {
        var assembly = NewAssembly([], availableTokens: 0, lockedTokens: 0);

        var snap = ReadinessProjector.Project(assembly, pendingProposalsCount: 0, pendingRunsCount: 0);

        snap.Status.Should().Be(ReadinessStatusNames.Empty);
        snap.SuggestedAction.Should().Be(ReadinessSuggestedActions.Wait);
        snap.AvailableTokens.Should().Be(0);
        snap.IntentCount.Should().Be(0);
    }

    [Fact(DisplayName = "Есть intents в окне → status=has_content, action=Run /dream")]
    public void Has_content_returns_run_action()
    {
        var assembly = NewAssembly(
            [SampleIntent("intent-1")],
            availableTokens: 250,
            lockedTokens: 0);

        var snap = ReadinessProjector.Project(assembly, pendingProposalsCount: 0, pendingRunsCount: 0);

        snap.Status.Should().Be(ReadinessStatusNames.HasContent);
        snap.SuggestedAction.Should().Be(ReadinessSuggestedActions.Run);
        snap.AvailableTokens.Should().Be(250);
        snap.IntentCount.Should().Be(1);
    }

    [Fact(DisplayName = "pending_runs_count > 0 имеет приоритет над has_content")]
    public void Pending_runs_override_status()
    {
        var assembly = NewAssembly(
            [SampleIntent("intent-1")],
            availableTokens: 100,
            lockedTokens: 50);

        var snap = ReadinessProjector.Project(assembly, pendingProposalsCount: 2, pendingRunsCount: 1);

        snap.Status.Should().Be(ReadinessStatusNames.PendingReview);
        snap.SuggestedAction.Should().Be(ReadinessSuggestedActions.Review);
        snap.LockedTokens.Should().Be(50);
        snap.PendingRunsCount.Should().Be(1);
        snap.PendingProposalsCount.Should().Be(2);
    }

    private static IntentInWindow SampleIntent(string id) =>
        new(id, "text", [], [], [], WindowStart.AddHours(1));

    private static DreamWindowAssembly NewAssembly(
        IReadOnlyList<IntentInWindow> available,
        int availableTokens,
        int lockedTokens)
    {
        var availableWindow = new IntentWindow(WindowStart, WindowEnd, available);
        var lockedWindow = new IntentWindow(WindowStart, WindowEnd, []);
        var breakdown = available
            .Select(i => new IntentTokenBreakdown(i.IntentId, availableTokens / Math.Max(1, available.Count), i.UpdatedAt))
            .ToList();
        return new DreamWindowAssembly(availableWindow, lockedWindow, availableTokens, lockedTokens, breakdown);
    }
}
