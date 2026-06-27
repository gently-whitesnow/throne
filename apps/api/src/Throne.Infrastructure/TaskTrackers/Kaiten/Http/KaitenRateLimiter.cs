namespace Throne.Infrastructure.TaskTrackers.Kaiten.Http;

/// <summary>
/// Process-wide token gate that keeps the adapter under Kaiten's request budget (~5 rps default).
/// A single <see cref="SemaphoreSlim"/> serialises the spacing check so concurrent callers — and
/// retries, which pass through here too — share one schedule rather than each pacing themselves.
/// Registered as a singleton so the limit is global, not per-call. Monotonic timing comes from
/// <see cref="TimeProvider"/> so the gap survives wall-clock jumps and is testable.
/// </summary>
internal sealed class KaitenRateLimiter(KaitenOptions options, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private long? _lastTimestamp;

    public async Task WaitAsync(CancellationToken ct)
    {
        var interval = Interval();
        if (interval <= TimeSpan.Zero)
        {
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_lastTimestamp is { } last)
            {
                var remaining = interval - clock.GetElapsedTime(last);
                if (remaining > TimeSpan.Zero)
                {
                    await Task.Delay(remaining, clock, ct);
                }
            }

            _lastTimestamp = clock.GetTimestamp();
        }
        finally
        {
            _gate.Release();
        }
    }

    private TimeSpan Interval() =>
        options.RequestsPerSecond <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(1.0 / options.RequestsPerSecond);

    public void Dispose() => _gate.Dispose();
}
