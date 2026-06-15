using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TmuxTuiReadinessWaiterTests
{
    private const string IntentId = "intent-readiness";

    [Fact(DisplayName = "Возвращает Ready на первом capture, если адаптер видит свой marker")]
    public async Task Returns_ready_when_vendor_marker_matches_first()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns("│ > composer ready");
        var adapter = new MarkerAdapter("│ >");
        var waiter = NewWaiter(tmux, timeoutMs: 1000, pollMs: 20);

        var result = await waiter.WaitAsync(IntentId, adapter, CancellationToken.None);

        result.IsReady.Should().BeTrue();
        result.Attempts.Should().Be(1);
        result.LastSnapshot.Should().BeNull();
        await tmux.Received(1).CapturePaneAsync(IntentId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Без vendor-marker'а готовность ловится по стабильности экрана между двумя capture")]
    public async Task Stability_fallback_signals_ready()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        // First capture: a transient splash banner. Second capture: same painted screen ->
        // stability fallback resolves to ready even though no vendor marker is present.
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns("splash painted", "splash painted");
        var adapter = new MarkerAdapter("never-matches");
        var waiter = NewWaiter(tmux, timeoutMs: 1000, pollMs: 20);

        var result = await waiter.WaitAsync(IntentId, adapter, CancellationToken.None);

        result.IsReady.Should().BeTrue();
        result.Attempts.Should().Be(2);
    }

    [Fact(DisplayName = "Пустой pane не считаем стабильным — иначе любой не запустившийся TUI выглядел бы готовым")]
    public async Task Empty_snapshots_do_not_trigger_stability()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(string.Empty);
        var adapter = new MarkerAdapter("never-matches");
        var waiter = NewWaiter(tmux, timeoutMs: 150, pollMs: 20);

        var result = await waiter.WaitAsync(IntentId, adapter, CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.LastSnapshot.Should().BeEmpty();
        result.Attempts.Should().BeGreaterThan(1);
    }

    [Fact(DisplayName = "Pane из одних переводов строк (TUI ещё не отрисовался) не должен ловиться стабильностью")]
    public async Task Whitespace_only_snapshots_do_not_trigger_stability()
    {
        // tmux capture-pane against a freshly-spawned TUI returns a column of newlines for the first
        // ~hundreds of ms — non-empty but visually blank. Treating those as "stable" was the bug
        // that lost the user prompt for Claude Code: the waiter resolved at t≈200ms and we pasted
        // into a pane that hadn't even rendered yet.
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(new string('\n', 50));
        var adapter = new MarkerAdapter("never-matches");
        var waiter = NewWaiter(tmux, timeoutMs: 150, pollMs: 20);

        var result = await waiter.WaitAsync(IntentId, adapter, CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.Attempts.Should().BeGreaterThan(1);
    }

    [Fact(DisplayName = "Если capture-pane всё время разный — таймаут, возвращаем последний snapshot для диагностики")]
    public async Task Returns_timeout_with_last_snapshot()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        var counter = 0;
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(_ => $"frame-{System.Threading.Interlocked.Increment(ref counter)}");
        var adapter = new MarkerAdapter("never-matches");
        var waiter = NewWaiter(tmux, timeoutMs: 150, pollMs: 20);

        var result = await waiter.WaitAsync(IntentId, adapter, CancellationToken.None);

        result.IsReady.Should().BeFalse();
        result.LastSnapshot.Should().StartWith("frame-");
        result.Attempts.Should().BeGreaterThan(1);
    }

    private static TmuxTuiReadinessWaiter NewWaiter(ITmuxSessionManager tmux, int timeoutMs, int pollMs)
    {
        var options = new RunPreflightOptions
        {
            TuiReadinessTimeoutMilliseconds = timeoutMs,
            TuiReadinessPollIntervalMilliseconds = pollMs,
        };
        return new TmuxTuiReadinessWaiter(
            tmux, options, TimeProvider.System, NullLogger<TmuxTuiReadinessWaiter>.Instance);
    }

    private sealed class MarkerAdapter(string marker) : ISessionHookAdapter
    {
        public string Vendor => "stub";

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId, string workspacePath, string mode, string? systemPrompt, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

        public bool IsTuiReady(string paneSnapshot) =>
            !string.IsNullOrEmpty(paneSnapshot)
            && paneSnapshot.Contains(marker, StringComparison.Ordinal);
    }
}
