using Microsoft.Extensions.Logging;

namespace Throne.Application.Terminals;

/// <summary>
/// Symmetric pair to <see cref="TmuxTuiReadinessWaiter"/> on the submit side. The readiness gate
/// guarantees the composer is up to accept bracketed-paste; this gate guarantees the trailing
/// <c>send-keys Enter</c> actually submitted the pasted prompt — Claude/Codex composers
/// occasionally absorb the Enter as a newline-in-paste when it arrives in the same render frame
/// as the closing <c>ESC[201~</c>, leaving the prompt sitting unsubmitted in the input row.
/// <list type="bullet">
///   <item>Polls <see cref="ITmuxSessionManager.CapturePaneAsync"/> at the readiness poll
///   interval and checks the vendor <see cref="ISessionHookAdapter.IsPromptSubmitted"/>
///   predicate — typically the appearance of a streaming/working footer that only renders
///   after Enter is honoured.</item>
///   <item>On timeout, re-sends a bare <c>Enter</c> (no re-paste — the buffer already landed in
///   the composer) and polls one more cycle. Bounded by
///   <see cref="RunPreflightOptions.PromptSubmitMaxRetries"/>; one retry by default.</item>
///   <item>Final timeout returns <see cref="TmuxPromptSubmitResult.Failed"/> with the last
///   captured snapshot so the caller can surface a diagnostic payload (same shape readiness
///   timeout uses today).</item>
/// </list>
/// </summary>
public sealed partial class TmuxPromptSubmitConfirmer(
    ITmuxSessionManager tmux,
    RunPreflightOptions options,
    TimeProvider clock,
    ILogger<TmuxPromptSubmitConfirmer> log)
{
    public async Task<TmuxPromptSubmitResult> ConfirmAsync(
        string intentId,
        ISessionHookAdapter adapter,
        string? submittedPrompt,
        TerminalPromptSubmitSignals.SubmitRegistration? submitSignal,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(adapter);

        var maxRetries = Math.Max(0, options.PromptSubmitMaxRetries);
        var pollTimeout = TimeSpan.FromMilliseconds(
            Math.Max(100, options.PromptSubmitConfirmTimeoutMilliseconds));
        var poll = TimeSpan.FromMilliseconds(
            Math.Max(20, options.TuiReadinessPollIntervalMilliseconds));

        var retries = 0;
        while (true)
        {
            var (confirmed, snapshot, attempts) = await PollOnceAsync(
                intentId, adapter, submittedPrompt, submitSignal, pollTimeout, poll, ct);
            if (confirmed)
            {
                LogConfirmed(log, intentId, adapter.Vendor, retries, attempts);
                return TmuxPromptSubmitResult.Confirmed(retries);
            }

            if (retries >= maxRetries)
            {
                LogFailed(log, intentId, adapter.Vendor, retries, attempts, pollTimeout.TotalMilliseconds);
                return TmuxPromptSubmitResult.Failed(retries, snapshot);
            }

            // Re-send Enter only (no re-paste): the prompt is already in the composer; just the
            // submit was lost. Counter-equivalent telemetry rides on the log warning so a stable
            // retry rate is visible without metric infra wired up — if rate > 0 persistently, the
            // root cause is premature readiness, not this gate's constant.
            retries++;
            LogRetry(log, intentId, adapter.Vendor, retries, attempts);
            await tmux.SendEnterAsync(intentId, ct);
        }
    }

    private async Task<(bool Confirmed, string Snapshot, int Attempts)> PollOnceAsync(
        string intentId,
        ISessionHookAdapter adapter,
        string? submittedPrompt,
        TerminalPromptSubmitSignals.SubmitRegistration? submitSignal,
        TimeSpan timeout,
        TimeSpan poll,
        CancellationToken ct)
    {
        var deadline = clock.GetUtcNow() + timeout;
        var snapshot = string.Empty;
        var attempts = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            // Authoritative path: the UserPromptSubmit hook fired, so the agent accepted the prompt.
            // Beats the pane scrape, which Claude muddies by echoing the pasted prompt back.
            if (submitSignal?.Submitted.IsCompleted == true)
            {
                return (true, snapshot, attempts);
            }

            attempts++;
            snapshot = await tmux.CapturePaneAsync(intentId, ct);
            if (adapter.IsPromptSubmitted(snapshot))
            {
                return (true, snapshot, attempts);
            }
            if (attempts > 1
                && !string.IsNullOrWhiteSpace(submittedPrompt)
                && adapter.IsTuiReady(snapshot)
                && !PromptStillVisible(snapshot, submittedPrompt))
            {
                return (true, snapshot, attempts);
            }

            if (clock.GetUtcNow() >= deadline)
            {
                return (false, snapshot, attempts);
            }

            var remaining = deadline - clock.GetUtcNow();
            var delay = remaining < poll ? remaining : poll;
            if (delay > TimeSpan.Zero)
            {
                await WaitNextPollAsync(submitSignal, delay, ct);
            }
        }
    }

    // Sleep one poll interval, but wake immediately if the UserPromptSubmit hook fires meanwhile.
    private async Task WaitNextPollAsync(
        TerminalPromptSubmitSignals.SubmitRegistration? submitSignal, TimeSpan delay, CancellationToken ct)
    {
        var delayTask = Task.Delay(delay, clock, ct);
        if (submitSignal is null)
        {
            await delayTask;
            return;
        }
        await Task.WhenAny(submitSignal.Submitted, delayTask);
    }

    private static bool PromptStillVisible(string snapshot, string? submittedPrompt)
    {
        if (string.IsNullOrWhiteSpace(snapshot) || string.IsNullOrWhiteSpace(submittedPrompt))
        {
            return false;
        }

        foreach (var fragment in PromptFragments(submittedPrompt))
        {
            if (snapshot.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<string> PromptFragments(string submittedPrompt)
    {
        const int minFragmentLength = 12;
        const int maxFragmentLength = 80;
        foreach (var line in submittedPrompt.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length < minFragmentLength)
            {
                continue;
            }
            yield return line.Length <= maxFragmentLength ? line : line[^maxFragmentLength..];
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "tmux prompt submit confirmed for intent {IntentId} ({Vendor}) after {Retries} retry(ies) / {Attempts} capture(s).")]
    private static partial void LogConfirmed(ILogger logger, string intentId, string vendor, int retries, int attempts);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "tmux prompt submit retry #{RetryCount} for intent {IntentId} ({Vendor}) after {Attempts} capture(s) — re-sending Enter.")]
    private static partial void LogRetry(ILogger logger, string intentId, string vendor, int retryCount, int attempts);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "tmux prompt submit gave up for intent {IntentId} ({Vendor}) after {Retries} retry(ies) / {Attempts} capture(s) / {TimeoutMs:0} ms per poll — Enter did not clear the composer.")]
    private static partial void LogFailed(ILogger logger, string intentId, string vendor, int retries, int attempts, double timeoutMs);
}
