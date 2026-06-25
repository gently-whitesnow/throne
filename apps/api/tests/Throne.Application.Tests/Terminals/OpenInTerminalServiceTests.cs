using FluentAssertions;
using NSubstitute;
using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Application.Terminals;
using Throne.Application.Terminals.Capabilities;
using Throne.Domain.Capabilities;
using Throne.Domain.Intents;
using CapabilitiesAggregate = Throne.Domain.Capabilities.Capabilities;

namespace Throne.Application.Tests.Terminals;

public class OpenInTerminalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 25, 10, 0, 0, TimeSpan.Zero);
    private const string IntentIdValue = "intent-native-1";

    [Fact(DisplayName = "Open выбирает persisted provider, требует live session и гасит embedded pipe")]
    public async Task Open_uses_selected_provider()
    {
        var fixture = new Fixture();
        fixture.IntentExists();
        fixture.LiveSession(true);
        fixture.SelectedProvider("wezterm");
        fixture.DetectionFor("wezterm", true);

        var result = await fixture.Service.OpenAsync(IntentIdValue, CancellationToken.None);

        result.ProviderName.Should().Be("wezterm");
        fixture.WezTerm.Opened.Should().ContainSingle().Which
            .Should().Be(("intent-native-1", "throne-intent-native-1"));
        await fixture.Tmux.Received(1).StopPipeAsync(IntentIdValue, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Open без live tmux session возвращает terminal.session_not_live")]
    public async Task Open_requires_live_session()
    {
        var fixture = new Fixture();
        fixture.IntentExists();
        fixture.LiveSession(false);

        var act = () => fixture.Service.OpenAsync(IntentIdValue, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(TerminalErrorCodes.SessionNotLive);
        fixture.WezTerm.Opened.Should().BeEmpty();
    }

    [Fact(DisplayName = "Open без selected provider фолбэчит на единственный detected")]
    public async Task Open_falls_back_to_single_detected_provider()
    {
        var fixture = new Fixture();
        fixture.IntentExists();
        fixture.LiveSession(true);
        fixture.NoPersisted();
        fixture.DetectionFor("wezterm", false);
        fixture.DetectionFor("apple_terminal", true);

        var result = await fixture.Service.OpenAsync(IntentIdValue, CancellationToken.None);

        result.ProviderName.Should().Be("apple_terminal");
        fixture.AppleTerminal.Opened.Should().ContainSingle();
    }

    [Fact(DisplayName = "Open без selected provider и с несколькими detected просит выбрать provider")]
    public async Task Open_rejects_ambiguous_detected_providers()
    {
        var fixture = new Fixture();
        fixture.IntentExists();
        fixture.LiveSession(true);
        fixture.NoPersisted();
        fixture.DetectionFor("wezterm", true);
        fixture.DetectionFor("apple_terminal", true);

        var act = () => fixture.Service.OpenAsync(IntentIdValue, CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ApiException>();
        ex.Which.Code.Should().Be(TerminalErrorCodes.NativeProviderUnavailable);
        fixture.WezTerm.Opened.Should().BeEmpty();
        fixture.AppleTerminal.Opened.Should().BeEmpty();
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Intents = Substitute.For<IIntentRepository>();
            Capabilities = Substitute.For<ICapabilitiesRepository>();
            Detection = Substitute.For<ICapabilityDetectionCache>();
            Tmux = Substitute.For<ITmuxSessionManager>();
            WezTerm = new FakeOpener("wezterm");
            AppleTerminal = new FakeOpener("apple_terminal");
            var persistence = new CapabilitiesPersistence(
                Capabilities,
                new PassthroughUnitOfWork(),
                new FixedClock(Now));
            Service = new OpenInTerminalService(
                persistence,
                Detection,
                new TerminalOpenerRegistry([WezTerm, AppleTerminal]),
                Intents,
                Tmux);
        }

        internal IIntentRepository Intents { get; }
        internal ICapabilitiesRepository Capabilities { get; }
        internal ICapabilityDetectionCache Detection { get; }
        internal ITmuxSessionManager Tmux { get; }
        internal FakeOpener WezTerm { get; }
        internal FakeOpener AppleTerminal { get; }
        internal OpenInTerminalService Service { get; }

        internal void IntentExists()
        {
            var intent = Intent.Restore(
                new IntentId(IntentIdValue), "x", IntentStatusNames.Work, 1, [], Now, Now);
            Intents.GetByIdAsync(Arg.Is<IntentId>(i => i.Value == IntentIdValue), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<Intent?>(intent));
        }

        internal void LiveSession(bool live) =>
            Tmux.HasSessionAsync(IntentIdValue, Arg.Any<CancellationToken>()).Returns(live);

        internal void NoPersisted() =>
            Capabilities.GetAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<CapabilitiesAggregate?>(null));

        internal void SelectedProvider(string provider)
        {
            var stored = CapabilitiesAggregate.CreateEmpty(Now);
            stored.SetSelectedProvider(CapabilityNames.OpenInTerminal, provider, Now);
            Capabilities.GetAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<CapabilitiesAggregate?>(stored));
        }

        internal void DetectionFor(string provider, bool detected) =>
            Detection.GetAsync(provider, Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<CapabilityProbeResult?>(
                    new CapabilityProbeResult(detected, detected ? "ok" : "missing")));
    }

    private sealed class FakeOpener(string providerName) : ITerminalOpener
    {
        public string ProviderName { get; } = providerName;
        public List<(string IntentId, string SessionName)> Opened { get; } = [];
        public Task<CapabilityProbeResult> ProbeAsync(CancellationToken ct) =>
            Task.FromResult(new CapabilityProbeResult(true, "ok"));

        public Task OpenAsync(string intentId, string sessionName, CancellationToken ct)
        {
            Opened.Add((intentId, sessionName));
            return Task.CompletedTask;
        }
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
