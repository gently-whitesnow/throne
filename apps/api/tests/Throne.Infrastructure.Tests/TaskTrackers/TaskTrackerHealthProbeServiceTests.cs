using NSubstitute;
using Throne.Application.Ports;
using Throne.Application.TaskTrackers;
using Throne.Infrastructure.TaskTrackers;

namespace Throne.Infrastructure.Tests.TaskTrackers;

public sealed class TaskTrackerHealthProbeServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ProbeAll probes each connected provider and persists the observed health")]
    public async Task ProbeAll_records_probe_health()
    {
        var provider = Substitute.For<ITaskTrackerConnectionProvider>();
        provider.TrackerKey.Returns("kaiten");
        provider.ProbeAsync(Arg.Any<TaskTrackerConnectionDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(TaskTrackerProbeResult.Offline("down"));

        var registry = Substitute.For<ITaskTrackerProviderRegistry>();
        registry.AllProviders.Returns([provider]);

        var store = Substitute.For<ITaskTrackerConnectionStore>();
        store.GetAsync("kaiten", Arg.Any<CancellationToken>())
            .Returns(new TaskTrackerStoredConnection("https://acme.kaiten.ru", "tok", []));

        await TaskTrackerHealthProbeService.ProbeAllAsync(
            registry, store, new FixedTimeProvider(Now), CancellationToken.None);

        await store.Received().SaveHealthAsync(
            "kaiten", TaskTrackerConnectionHealth.Offline, "down", Now, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ProbeAll skips providers with no saved connection (no phantom health)")]
    public async Task ProbeAll_skips_unconnected_provider()
    {
        var provider = Substitute.For<ITaskTrackerConnectionProvider>();
        provider.TrackerKey.Returns("kaiten");

        var registry = Substitute.For<ITaskTrackerProviderRegistry>();
        registry.AllProviders.Returns([provider]);

        var store = Substitute.For<ITaskTrackerConnectionStore>();
        store.GetAsync("kaiten", Arg.Any<CancellationToken>())
            .Returns((TaskTrackerStoredConnection?)null);

        await TaskTrackerHealthProbeService.ProbeAllAsync(
            registry, store, new FixedTimeProvider(Now), CancellationToken.None);

        await provider.DidNotReceive().ProbeAsync(
            Arg.Any<TaskTrackerConnectionDescriptor>(), Arg.Any<CancellationToken>());
        await store.DidNotReceive().SaveHealthAsync(
            Arg.Any<string>(), Arg.Any<TaskTrackerConnectionHealth>(), Arg.Any<string?>(),
            Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
