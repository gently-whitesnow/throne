using Microsoft.Extensions.Logging;

namespace Throne.Application.Terminals;

/// <summary>
/// Waits until the vendor TUI is ready to receive bracketed-paste input, then returns. Three
/// complementary signals make the wait robust to vendor cosmetic churn and plugin failures:
/// <list type="bullet">
///   <item>provider-native readiness hook (<see cref="ISessionHookAdapter.ReadinessHookEvent"/>)
///   — the authoritative "I'm up" signal from the agent itself (OpenCode <c>session.created</c>);
///   followed by a one-poll settle because the hook can fire a beat before the composer actually
///   accepts paste;</item>
///   <item>vendor glyph marker via <see cref="ISessionHookAdapter.IsTuiReady"/> — fast path for
///   vendors without a readiness event (Claude <c>❯</c>, Codex);</item>
///   <item>screen-stability fallback — two consecutive non-empty captures that are identical mean
///   the TUI stopped painting, so paste is safe even with no event and no recognised glyph.</item>
/// </list>
/// </summary>
public sealed partial class TmuxTuiReadinessWaiter(
    ITmuxSessionManager tmux,
    RunPreflightOptions options,
    TimeProvider clock,
    ILogger<TmuxTuiReadinessWaiter> log)
{
    public async Task<TmuxTuiReadinessResult> WaitAsync(
        string intentId,
        ISessionHookAdapter adapter,
        TerminalReadinessSignals.TerminalReadinessRegistration? readiness,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intentId);
        ArgumentNullException.ThrowIfNull(adapter);

        var timeout = TimeSpan.FromMilliseconds(Math.Max(100, options.TuiReadinessTimeoutMilliseconds));
        var poll = TimeSpan.FromMilliseconds(Math.Max(20, options.TuiReadinessPollIntervalMilliseconds));
        var deadline = clock.GetUtcNow() + timeout;

        string previous = string.Empty;
        var attempts = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (readiness?.Ready.IsCompleted == true)
            {
                await SettleAfterSignalAsync(poll, ct);
                LogReadyBySignal(log, intentId, adapter.Vendor, attempts);
                return TmuxTuiReadinessResult.Ready(attempts);
            }

            attempts++;
            var snapshot = await tmux.CapturePaneAsync(intentId, ct);
            // Vendor glyph fast-path: vendors without a provider-native readiness event (Claude `❯`,
            // Codex) still recognise their composer marker on the captured pane. Adapters that own a
            // readiness hook return false here and resolve via the signal/stability paths instead.
            if (adapter.IsTuiReady(snapshot))
            {
                LogReadyByMarker(log, intentId, adapter.Vendor, attempts);
                return TmuxTuiReadinessResult.Ready(attempts);
            }
            // Whitespace-only captures (an unpainted pane comes back as ~h newlines) must NOT count as
            // stable: at t≈100 ms tmux returns blanks, at t≈200 ms it returns the same blanks, and
            // the fallback would fire before the TUI has rendered anything to receive the paste.
            if (!string.IsNullOrWhiteSpace(snapshot)
                && string.Equals(snapshot, previous, StringComparison.Ordinal))
            {
                LogReadyByStability(log, intentId, adapter.Vendor, attempts);
                return TmuxTuiReadinessResult.Ready(attempts);
            }

            if (clock.GetUtcNow() >= deadline)
            {
                LogTimeout(log, intentId, adapter.Vendor, attempts, timeout.TotalMilliseconds);
                return TmuxTuiReadinessResult.TimedOut(attempts, snapshot);
            }

            previous = snapshot;
            var remaining = deadline - clock.GetUtcNow();
            var delay = remaining < poll ? remaining : poll;
            if (delay <= TimeSpan.Zero)
            {
                continue;
            }

            var delayTask = Task.Delay(delay, clock, ct);
            if (readiness is null)
            {
                await delayTask;
                continue;
            }

            var completed = await Task.WhenAny(readiness.Ready, delayTask);
            if (completed == readiness.Ready)
            {
                await SettleAfterSignalAsync(poll, ct);
                LogReadyBySignal(log, intentId, adapter.Vendor, attempts);
                return TmuxTuiReadinessResult.Ready(attempts);
            }
        }
    }

    // The readiness hook (e.g. OpenCode session.created) can fire a beat before the composer
    // actually accepts bracketed-paste, so a bare signal-triggered return would re-introduce the
    // lost-prompt race the hook set out to kill — just under a new trigger. One poll-interval settle
    // lets the composer finish painting before paste. It is bounded and unconditional, so the signal
    // stays authoritative — unlike waiting for screen stability, which could falsely time out if the
    // pane keeps repainting after the agent reports ready.
    private Task SettleAfterSignalAsync(TimeSpan poll, CancellationToken ct) =>
        Task.Delay(poll, clock, ct);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "tmux TUI ready for intent {IntentId} ({Vendor}): readiness hook fired after {Attempts} capture(s).")]
    private static partial void LogReadyBySignal(ILogger logger, string intentId, string vendor, int attempts);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "tmux TUI ready for intent {IntentId} ({Vendor}): vendor glyph marker matched after {Attempts} capture(s).")]
    private static partial void LogReadyByMarker(ILogger logger, string intentId, string vendor, int attempts);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "tmux TUI ready for intent {IntentId} ({Vendor}): screen stable after {Attempts} capture(s).")]
    private static partial void LogReadyByStability(ILogger logger, string intentId, string vendor, int attempts);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "tmux TUI readiness gave up for intent {IntentId} ({Vendor}) after {Attempts} captures / {TimeoutMs:0} ms — pane never rendered a ready composer.")]
    private static partial void LogTimeout(ILogger logger, string intentId, string vendor, int attempts, double timeoutMs);
}

/// <summary>
/// Outcome of one <see cref="TmuxTuiReadinessWaiter.WaitAsync"/> pass. <see cref="LastSnapshot"/>
/// is the final capture and is non-null only on timeout — used by the diagnostic payload so a
/// stuck TUI never looks like a successful spawn.
/// </summary>
public sealed record TmuxTuiReadinessResult(bool IsReady, int Attempts, string? LastSnapshot)
{
    public static TmuxTuiReadinessResult Ready(int attempts) => new(true, attempts, null);
    public static TmuxTuiReadinessResult TimedOut(int attempts, string snapshot) =>
        new(false, attempts, snapshot);
}
