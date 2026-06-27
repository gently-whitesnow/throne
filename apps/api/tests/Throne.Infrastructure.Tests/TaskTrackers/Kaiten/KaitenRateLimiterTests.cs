using System.Diagnostics;
using FluentAssertions;
using Throne.Infrastructure.TaskTrackers.Kaiten;
using Throne.Infrastructure.TaskTrackers.Kaiten.Http;

namespace Throne.Infrastructure.Tests.TaskTrackers.Kaiten;

public class KaitenRateLimiterTests
{
    [Fact(DisplayName = "rps<=0 — троттлинга нет, оба вызова мгновенны")]
    public async Task Disabled_when_non_positive_rate()
    {
        using var limiter = new KaitenRateLimiter(new KaitenOptions { RequestsPerSecond = 0 }, TimeProvider.System);

        var sw = Stopwatch.StartNew();
        await limiter.WaitAsync(CancellationToken.None);
        await limiter.WaitAsync(CancellationToken.None);
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    [Fact(DisplayName = "Второй вызов выдерживает паузу под заданный rps")]
    public async Task Spaces_consecutive_requests()
    {
        // 20 rps → ~50ms interval. First call is free; the second must wait out the gap.
        using var limiter = new KaitenRateLimiter(new KaitenOptions { RequestsPerSecond = 20 }, TimeProvider.System);

        await limiter.WaitAsync(CancellationToken.None);
        var sw = Stopwatch.StartNew();
        await limiter.WaitAsync(CancellationToken.None);
        sw.Stop();

        sw.Elapsed.Should().BeGreaterThan(TimeSpan.FromMilliseconds(25));
    }
}
