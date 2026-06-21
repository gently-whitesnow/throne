using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Tests.Terminals;

public class CapabilitiesServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "List: essentials enabled=detected (auto-ready), опциональные — enabled=false при пустом singleton")]
    public async Task List_returns_descriptors_with_persisted_defaults()
    {
        var fixture = new ServiceFixture();
        fixture.NoPersisted();
        fixture.DetectionFor(CapabilityNames.Terminal, detected: true, detail: "tmux 3.5a");
        fixture.DetectionFor(CapabilityNames.Repositories, detected: false, detail: "gh not authenticated");
        fixture.DetectionFor(CapabilityNames.Gitlab, detected: false, detail: "glab not configured");
        fixture.DetectionFor(CapabilityNames.Vscode, detected: true, detail: "1.95.3");

        var views = await fixture.Service.ListAsync(CancellationToken.None);

        views.Select(v => v.Name).Should().Equal(
            CapabilityNames.Repositories,
            CapabilityNames.Gitlab,
            CapabilityNames.Terminal,
            CapabilityNames.Vscode,
            CapabilityNames.Opencode);
        // Essential terminal is detection-ready (ADR-0026 § 9) → enabled mirrors detected.
        views.Single(v => v.Name == CapabilityNames.Terminal).Enabled.Should().BeTrue();
        views.Single(v => v.Name == CapabilityNames.Terminal).Detected.Should().BeTrue();
        views.Single(v => v.Name == CapabilityNames.Terminal).DetectionDetail.Should().Be("tmux 3.5a");
        // Essential repositories undetected → disabled.
        views.Single(v => v.Name == CapabilityNames.Repositories).Enabled.Should().BeFalse();
        views.Single(v => v.Name == CapabilityNames.Repositories).Detected.Should().BeFalse();
        // Optional vscode: detected but no explicit opt-in → still disabled.
        views.Single(v => v.Name == CapabilityNames.Vscode).Enabled.Should().BeFalse();
    }

    [Fact(DisplayName = "List: опциональная capability отдаёт persisted enabled; essential игнорирует toggle")]
    public async Task List_returns_persisted_enabled_toggle()
    {
        var fixture = new ServiceFixture();
        var stored = CapabilitiesAggregate.CreateEmpty(Now);
        stored.SetEnabled(CapabilityNames.Vscode, true, Now);
        stored.SetEnabled(CapabilityNames.Terminal, true, Now);
        fixture.Persisted(stored);
        fixture.DetectionMissing();

        var views = await fixture.Service.ListAsync(CancellationToken.None);

        // Optional vscode reflects the persisted toggle regardless of detection.
        views.Single(v => v.Name == CapabilityNames.Vscode).Enabled.Should().BeTrue();
        // Essential terminal ignores the stored toggle — undetected → disabled.
        views.Single(v => v.Name == CapabilityNames.Terminal).Enabled.Should().BeFalse();
    }

    [Fact(DisplayName = "IsAvailable: essential terminal доступен по detection без opt-in; opt-in без detection — нет")]
    public async Task IsAvailable_essential_is_detection_ready()
    {
        var fixture = new ServiceFixture();
        fixture.NoPersisted();
        fixture.DetectionFor(CapabilityNames.Terminal, detected: true, detail: "tmux 3.5a");
        fixture.DetectionFor(CapabilityNames.Vscode, detected: true, detail: "1.95.3");

        // Essential: detected → available even though nothing was toggled on.
        (await fixture.Service.IsAvailableAsync(CapabilityNames.Terminal, CancellationToken.None))
            .Should().BeTrue();
        // Optional: detected but not toggled on → unavailable.
        (await fixture.Service.IsAvailableAsync(CapabilityNames.Vscode, CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact(DisplayName = "Toggle с неизвестным name бросает capability.not_found")]
    public async Task Toggle_unknown_capability_throws()
    {
        var fixture = new ServiceFixture();
        fixture.NoPersisted();

        var act = () => fixture.Service.ToggleAsync("jira", enabled: true, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityNotFound);
    }

    [Fact(DisplayName = "Toggle материализует singleton при первом включении и сохраняет toggle=true")]
    public async Task Toggle_materializes_singleton_on_first_write()
    {
        var fixture = new ServiceFixture();
        // Repository starts empty; after SaveAsync we flip the GetAsync stub to
        // return the persisted aggregate so the post-write view reflects it.
        fixture.RecordSavesAndReadAfter();
        fixture.DetectionMissing();

        // Optional capability: the returned view reflects the persisted toggle (essentials
        // would instead report enabled=detected, masking the toggle — see auto-ready tests).
        var view = await fixture.Service.ToggleAsync(CapabilityNames.Vscode, enabled: true, CancellationToken.None);

        view.Enabled.Should().BeTrue();
        await fixture.Repository.Received().SaveAsync(
            Arg.Is<CapabilitiesAggregate>(c => c.IsEnabled(CapabilityNames.Vscode)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Toggle no-op (тот же enabled) не делает SaveAsync")]
    public async Task Toggle_no_op_skips_save()
    {
        var fixture = new ServiceFixture();
        var stored = CapabilitiesAggregate.CreateEmpty(Now);
        stored.SetEnabled(CapabilityNames.Terminal, true, Now);
        fixture.Persisted(stored);
        fixture.DetectionMissing();

        await fixture.Service.ToggleAsync(CapabilityNames.Terminal, enabled: true, CancellationToken.None);

        await fixture.Repository.DidNotReceive().SaveAsync(Arg.Any<CapabilitiesAggregate>(), Arg.Any<CancellationToken>());
    }

    private sealed class ServiceFixture
    {
        public ServiceFixture()
        {
            Repository = Substitute.For<ICapabilitiesRepository>();
            Detection = Substitute.For<ICapabilityDetectionCache>();
            var clock = new FixedClock(Now);
            var unitOfWork = new PassthroughUnitOfWork();
            var persistence = new CapabilitiesPersistence(Repository, unitOfWork, clock);
            Service = new CapabilitiesService(persistence, Detection);
        }

        public ICapabilitiesRepository Repository { get; }
        public ICapabilityDetectionCache Detection { get; }
        public CapabilitiesService Service { get; }

        public void NoPersisted() =>
            Repository.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CapabilitiesAggregate?>(null));

        public void Persisted(CapabilitiesAggregate stored) =>
            Repository.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CapabilitiesAggregate?>(stored));

        public void RecordSavesAndReadAfter()
        {
            CapabilitiesAggregate? stored = null;
            Repository.GetAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(stored));
            Repository.SaveAsync(Arg.Any<CapabilitiesAggregate>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    stored = ci.Arg<CapabilitiesAggregate>();
                    return Task.CompletedTask;
                });
        }

        public void DetectionMissing() =>
            Detection.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<CapabilityProbeResult?>(null));

        public void DetectionFor(string name, bool detected, string detail) =>
            Detection.GetAsync(name, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<CapabilityProbeResult?>(new CapabilityProbeResult(detected, detail)));
    }

    private sealed class PassthroughUnitOfWork : IUnitOfWork
    {
        public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
        public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct) => work(ct);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
