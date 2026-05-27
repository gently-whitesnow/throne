using FluentAssertions;
using Microsoft.Extensions.Options;
using Throne.Application.Terminals.Capabilities;
using Throne.Infrastructure.Terminals.Capabilities;

namespace Throne.Infrastructure.Tests.Terminals;

public class CapabilityDetectionCacheTests
{
    [Fact(DisplayName = "Cache переиспользует результат внутри TTL")]
    public async Task Reuses_within_ttl()
    {
        var probe = new RecordingProbe("terminal", new CapabilityProbeResult(true, "tmux 3.5"));
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new CapabilityDetectionCache(
            [probe],
            Options.Create(new CapabilityDetectionOptions { DetectionTtlSeconds = 60 }),
            clock);

        var first = await cache.GetAsync("terminal", CancellationToken.None);
        var second = await cache.GetAsync("terminal", CancellationToken.None);

        first.Should().Be(second);
        probe.CallCount.Should().Be(1);
    }

    [Fact(DisplayName = "Cache перепроверяет после истечения TTL")]
    public async Task Re_probes_after_ttl()
    {
        var probe = new RecordingProbe("terminal", new CapabilityProbeResult(true, "tmux 3.5"));
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new CapabilityDetectionCache(
            [probe],
            Options.Create(new CapabilityDetectionOptions { DetectionTtlSeconds = 1 }),
            clock);

        await cache.GetAsync("terminal", CancellationToken.None);
        clock.Advance(TimeSpan.FromSeconds(2));
        await cache.GetAsync("terminal", CancellationToken.None);

        probe.CallCount.Should().Be(2);
    }

    [Fact(DisplayName = "Cache возвращает null для неизвестной capability")]
    public async Task Unknown_capability_returns_null()
    {
        var probe = new RecordingProbe("terminal", new CapabilityProbeResult(true, "tmux"));
        var cache = new CapabilityDetectionCache(
            [probe],
            Options.Create(new CapabilityDetectionOptions()),
            new FakeClock(DateTimeOffset.UtcNow));

        var result = await cache.GetAsync("jira", CancellationToken.None);

        result.Should().BeNull();
    }

    private sealed class RecordingProbe(string name, CapabilityProbeResult result) : ICapabilityProbe
    {
        public string CapabilityName => name;
        public int CallCount { get; private set; }

        public Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
