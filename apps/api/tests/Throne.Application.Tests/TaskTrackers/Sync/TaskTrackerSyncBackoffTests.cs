using FluentAssertions;
using Throne.Application.TaskTrackers.Sync;

namespace Throne.Application.Tests.TaskTrackers.Sync;

public class TaskTrackerSyncBackoffTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static TaskTrackerSyncBackoff Backoff(int initial = 30, int max = 120) =>
        new(new TaskTrackerSyncOptions { BackoffInitialSeconds = initial, BackoffMaxSeconds = max });

    [Fact(DisplayName = "ShouldSkip: false для ключа без зафиксированных сбоев")]
    public void Skip_false_without_failure()
    {
        Backoff().ShouldSkip("board-1", Now).Should().BeFalse();
    }

    [Fact(DisplayName = "RecordFailure ставит окно на initial секунд")]
    public void First_failure_uses_initial_delay()
    {
        var backoff = Backoff(initial: 30);
        backoff.RecordFailure("board-1", Now);

        backoff.ShouldSkip("board-1", Now.AddSeconds(29)).Should().BeTrue();
        backoff.ShouldSkip("board-1", Now.AddSeconds(31)).Should().BeFalse();
    }

    [Fact(DisplayName = "Повторные сбои удваивают задержку")]
    public void Repeated_failures_double()
    {
        var backoff = Backoff(initial: 30, max: 1000);
        backoff.RecordFailure("board-1", Now); // 30
        backoff.RecordFailure("board-1", Now); // 60
        backoff.RecordFailure("board-1", Now); // 120

        backoff.ShouldSkip("board-1", Now.AddSeconds(119)).Should().BeTrue();
        backoff.ShouldSkip("board-1", Now.AddSeconds(121)).Should().BeFalse();
    }

    [Fact(DisplayName = "Удвоение упирается в BackoffMaxSeconds")]
    public void Doubling_capped_at_max()
    {
        var backoff = Backoff(initial: 30, max: 100);
        for (var i = 0; i < 10; i++)
        {
            backoff.RecordFailure("board-1", Now);
        }

        backoff.ShouldSkip("board-1", Now.AddSeconds(99)).Should().BeTrue();
        backoff.ShouldSkip("board-1", Now.AddSeconds(101)).Should().BeFalse();
    }

    [Fact(DisplayName = "RecordSuccess сбрасывает окно бэкоффа")]
    public void Success_clears_backoff()
    {
        var backoff = Backoff(initial: 30);
        backoff.RecordFailure("board-1", Now);

        backoff.RecordSuccess("board-1");

        backoff.ShouldSkip("board-1", Now.AddSeconds(1)).Should().BeFalse();
    }

    [Fact(DisplayName = "Бэкофф независим по ключам досок")]
    public void Backoff_is_per_key()
    {
        var backoff = Backoff(initial: 30);
        backoff.RecordFailure("board-1", Now);

        backoff.ShouldSkip("board-2", Now).Should().BeFalse();
    }
}
