using Microsoft.Extensions.Logging;

namespace Throne.Application.Terminals;

/// <summary>
/// Polls <see cref="ITmuxSessionManager.CapturePaneAsync"/> until the vendor TUI has rendered
/// a composer ready to receive bracketed-paste input, then returns. Replaces the historical
/// blind <c>BracketedPasteWarmup</c> sleep that lost prompts whenever the TUI took longer to
/// init than the hard-coded delay (Claude Code on this box needs 1200–1500 ms cold; the old
/// 300 ms warmup silently dropped the user prompt — see ADR-0026 follow-up).
///
/// Two complementary signals make the wait robust to vendor cosmetic churn:
/// <list type="bullet">
///   <item>vendor-specific marker via <see cref="ISessionHookAdapter.IsTuiReady"/> — fast path
///   when the TUI's composer glyph is recognised on the very first capture;</item>
///   <item>screen-stability fallback — when two consecutive non-empty captures are identical,
///   the TUI has stopped painting and is steady, so paste is safe even if the vendor changed
///   the marker glyph between releases.</item>
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
            attempts++;
            var snapshot = await tmux.CapturePaneAsync(intentId, ct);
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
            await Task.Delay(poll, clock, ct);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "tmux TUI ready for intent {IntentId} ({Vendor}): vendor marker matched after {Attempts} capture(s).")]
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
