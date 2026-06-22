using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Throne.Application.Terminals;

namespace Throne.Application.Tests.Terminals;

public class TmuxPromptSubmitConfirmerTests
{
    private const string IntentId = "intent-submit-1";
    private const string WorkingFooter = "✻ Tinkering… esc to interrupt";

    [Fact(DisplayName = "Happy-path: composer показывает working footer на первом capture — Confirmed без retry")]
    public async Task Confirms_on_first_capture_without_retry()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(WorkingFooter);
        var adapter = new EscToInterruptAdapter();
        var confirmer = NewConfirmer(tmux, confirmTimeoutMs: 200, maxRetries: 1);

        var result = await confirmer.ConfirmAsync(
            IntentId, adapter, submittedPrompt: null, submitSignal: null, CancellationToken.None);

        result.IsConfirmed.Should().BeTrue();
        result.Retries.Should().Be(0);
        result.LastSnapshot.Should().BeNull();
        await tmux.DidNotReceiveWithAnyArgs().SendEnterAsync(default!, default);
    }

    [Fact(DisplayName = "Первый Enter потерян: confirmer re-send-ит Enter и подтверждает на втором цикле")]
    public async Task Retries_enter_once_when_first_submit_lost()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        // The composer only flips to the working footer AFTER the retry Enter lands — until then
        // capture-pane returns the pre-submit shape no matter how often it is polled, so the
        // first poll cycle times out and forces the retry.
        var enterSent = false;
        tmux.SendEnterAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(_ => { enterSent = true; return Task.CompletedTask; });
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns(_ => enterSent ? WorkingFooter : "❯ pasted prompt still in composer");
        var adapter = new EscToInterruptAdapter();
        var confirmer = NewConfirmer(tmux, confirmTimeoutMs: 100, maxRetries: 1);

        var result = await confirmer.ConfirmAsync(
            IntentId, adapter, submittedPrompt: null, submitSignal: null, CancellationToken.None);

        result.IsConfirmed.Should().BeTrue();
        result.Retries.Should().Be(1);
        await tmux.Received(1).SendEnterAsync(IntentId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Submit не подтверждён ни разу — возвращается Failed с последним snapshot для диагностики")]
    public async Task Final_timeout_returns_failed_with_last_snapshot()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns("❯ pasted prompt still in composer");
        var adapter = new EscToInterruptAdapter();
        var confirmer = NewConfirmer(tmux, confirmTimeoutMs: 100, maxRetries: 1);

        var result = await confirmer.ConfirmAsync(
            IntentId, adapter, submittedPrompt: null, submitSignal: null, CancellationToken.None);

        result.IsConfirmed.Should().BeFalse();
        result.Retries.Should().Be(1);
        result.LastSnapshot.Should().Contain("pasted prompt still in composer");
        // The retry path must have re-sent Enter once before giving up.
        await tmux.Received(1).SendEnterAsync(IntentId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Prompt успел завершиться до footer capture — пустой composer подтверждает submit без retry")]
    public async Task Confirms_when_composer_is_cleared_before_working_footer_is_seen()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns("❯", "❯");
        var adapter = new EscToInterruptAdapter();
        var confirmer = NewConfirmer(tmux, confirmTimeoutMs: 200, maxRetries: 1);

        var result = await confirmer.ConfirmAsync(
            IntentId,
            adapter,
            submittedPrompt: "please run this distinctive prompt",
            submitSignal: null,
            CancellationToken.None);

        result.IsConfirmed.Should().BeTrue();
        result.Retries.Should().Be(0);
        await tmux.Received(2).CapturePaneAsync(IntentId, Arg.Any<CancellationToken>());
        await tmux.DidNotReceiveWithAnyArgs().SendEnterAsync(default!, default);
    }

    [Fact(DisplayName = "UserPromptSubmit hook бьёт scrape: Confirmed даже когда pane не показал working footer")]
    public async Task Hook_signal_confirms_before_pane_scrape()
    {
        var tmux = Substitute.For<ITmuxSessionManager>();
        // The pane never flips to the working footer — the only path to Confirmed is the hook signal.
        tmux.CapturePaneAsync(IntentId, Arg.Any<CancellationToken>())
            .Returns("❯ pasted prompt still in composer");
        var adapter = new EscToInterruptAdapter();
        var confirmer = NewConfirmer(tmux, confirmTimeoutMs: 200, maxRetries: 1);

        var submitSignals = new TerminalPromptSubmitSignals();
        using var registration = submitSignals.Arm(IntentId);
        // The authoritative UserPromptSubmit hook fired.
        submitSignals.TrySignal(IntentId).Should().BeTrue();

        var result = await confirmer.ConfirmAsync(
            IntentId, adapter, submittedPrompt: null, submitSignal: registration, CancellationToken.None);

        result.IsConfirmed.Should().BeTrue();
        result.Retries.Should().Be(0);
        await tmux.DidNotReceiveWithAnyArgs().SendEnterAsync(default!, default);
    }

    private static TmuxPromptSubmitConfirmer NewConfirmer(
        ITmuxSessionManager tmux, int confirmTimeoutMs, int maxRetries)
    {
        var options = new RunPreflightOptions
        {
            TuiReadinessPollIntervalMilliseconds = 20,
            PromptSubmitConfirmTimeoutMilliseconds = confirmTimeoutMs,
            PromptSubmitMaxRetries = maxRetries,
        };
        return new TmuxPromptSubmitConfirmer(
            tmux, options, TimeProvider.System,
            NullLogger<TmuxPromptSubmitConfirmer>.Instance);
    }

    private sealed class EscToInterruptAdapter : ISessionHookAdapter
    {
        public string Vendor => "stub";

        public Task<IReadOnlyList<string>> PrepareSpawnArgsAsync(
            string intentId, string workspacePath, string mode, string? systemPrompt,
            IReadOnlyList<SessionSkillPackage> skillPackages, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task CleanupAsync(string intentId, CancellationToken ct) => Task.CompletedTask;

        public bool IsTuiReady(string paneSnapshot) =>
            !string.IsNullOrEmpty(paneSnapshot)
            && paneSnapshot.Contains('❯');

        public bool IsPromptSubmitted(string paneSnapshot) =>
            !string.IsNullOrEmpty(paneSnapshot)
            && paneSnapshot.Contains("esc to interrupt", StringComparison.OrdinalIgnoreCase);
    }
}
