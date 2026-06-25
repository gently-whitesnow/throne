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

    [Fact(DisplayName = "List: open_in_ide with no persisted selection and one detected provider")]
    public async Task List_returns_descriptor_with_provider_views()
    {
        var fixture = new ServiceFixture();
        fixture.NoPersisted();
        fixture.DetectionFor("vscode", detected: true, detail: "1.95.3");
        fixture.DetectionFor("cursor", detected: false, detail: "cursor CLI not found on PATH");

        var views = await fixture.Service.ListAsync(CancellationToken.None);

        views.Select(v => v.Name).Should().Contain([CapabilityNames.OpenInIde, CapabilityNames.OpenInTerminal]);
        var view = views.Single(v => v.Name == CapabilityNames.OpenInIde);
        view.SelectedProvider.Should().BeNull();
        view.Providers.Select(p => p.Name).Should().Contain(["vscode", "cursor"]);
        view.Providers.Single(p => p.Name == "vscode").Detected.Should().BeTrue();
        view.Providers.Single(p => p.Name == "cursor").Detected.Should().BeFalse();
    }

    [Fact(DisplayName = "List: returns persisted selected_provider")]
    public async Task List_returns_persisted_selection()
    {
        var fixture = new ServiceFixture();
        var stored = CapabilitiesAggregate.CreateEmpty(Now);
        stored.SetSelectedProvider(CapabilityNames.OpenInIde, "cursor", Now);
        fixture.Persisted(stored);
        fixture.DetectionMissing();

        var views = await fixture.Service.ListAsync(CancellationToken.None);

        views.Single(v => v.Name == CapabilityNames.OpenInIde).SelectedProvider.Should().Be("cursor");
    }

    [Fact(DisplayName = "SetSelectedProvider with unknown capability throws capability.not_found")]
    public async Task Set_unknown_capability_throws()
    {
        var fixture = new ServiceFixture();
        fixture.NoPersisted();

        var act = () => fixture.Service.SetSelectedProviderAsync("jira", "vscode", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityNotFound);
    }

    [Fact(DisplayName = "SetSelectedProvider with unknown provider throws capability.provider_not_found")]
    public async Task Set_unknown_provider_throws()
    {
        var fixture = new ServiceFixture();
        fixture.NoPersisted();

        var act = () => fixture.Service.SetSelectedProviderAsync(
            CapabilityNames.OpenInIde, "emacs", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(ErrorCodes.CapabilityProviderNotFound);
    }

    [Fact(DisplayName = "SetSelectedProvider materialises singleton and persists provider on first write")]
    public async Task Set_materialises_singleton_on_first_write()
    {
        var fixture = new ServiceFixture();
        fixture.RecordSavesAndReadAfter();
        fixture.DetectionMissing();

        var view = await fixture.Service.SetSelectedProviderAsync(
            CapabilityNames.OpenInIde, "vscode", CancellationToken.None);

        view.SelectedProvider.Should().Be("vscode");
        await fixture.Repository.Received().SaveAsync(
            Arg.Is<CapabilitiesAggregate>(c => c.GetSelectedProvider(CapabilityNames.OpenInIde) == "vscode"),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "SetSelectedProvider no-op (same provider) does not call SaveAsync")]
    public async Task Set_no_op_skips_save()
    {
        var fixture = new ServiceFixture();
        var stored = CapabilitiesAggregate.CreateEmpty(Now);
        stored.SetSelectedProvider(CapabilityNames.OpenInIde, "vscode", Now);
        fixture.Persisted(stored);
        fixture.DetectionMissing();

        await fixture.Service.SetSelectedProviderAsync(
            CapabilityNames.OpenInIde, "vscode", CancellationToken.None);

        await fixture.Repository.DidNotReceive().SaveAsync(
            Arg.Any<CapabilitiesAggregate>(), Arg.Any<CancellationToken>());
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
