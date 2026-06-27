using System.Net;
using System.Net.Http.Headers;

namespace Throne.Infrastructure.TaskTrackers.Kaiten.Http;

/// <summary>
/// Decides whether and how long to wait before retrying a failed Kaiten request, mirroring the
/// reference client: retry on 429 and 5xx only, honour a <c>Retry-After</c> hint when present, and
/// otherwise back off exponentially up to a cap. Pure decision logic — no I/O, no sleeping — so the
/// retry contract is unit-tested without the network.
/// </summary>
internal sealed class KaitenRetryPolicy(KaitenOptions options)
{
    public static bool IsRetryable(HttpStatusCode status) =>
        status == HttpStatusCode.TooManyRequests || ((int)status >= 500 && (int)status <= 599);

    /// <summary>Delay before the next attempt. <paramref name="attempt"/> is 1-based (1 = first retry).</summary>
    public TimeSpan NextDelay(int attempt, HttpResponseMessage response, DateTimeOffset now) =>
        ReadRetryAfter(response.Headers.RetryAfter, now) ?? Backoff(attempt);

    public TimeSpan Backoff(int attempt)
    {
        var factor = Math.Pow(2, Math.Max(0, attempt - 1));
        var millis = options.RetryBaseDelayMilliseconds * factor;
        var capped = Math.Min(millis, options.RetryMaxDelayMilliseconds);
        return TimeSpan.FromMilliseconds(capped);
    }

    private static TimeSpan? ReadRetryAfter(RetryConditionHeaderValue? header, DateTimeOffset now)
    {
        if (header is null)
        {
            return null;
        }

        if (header.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (header.Date is { } date && date - now > TimeSpan.Zero)
        {
            return date - now;
        }

        return null;
    }
}
