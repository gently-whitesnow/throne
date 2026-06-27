namespace Throne.Infrastructure.TaskTrackers.Kaiten;

/// <summary>
/// Reliability knobs for the Kaiten HTTP client. Defaults mirror the reference CLI: ~5 rps and up to
/// three attempts with exponential backoff capped at five seconds. Bound from
/// <c>Throne:TaskTrackers:Kaiten</c>; the defaults are production-safe so no configuration is
/// required to boot.
/// </summary>
internal sealed class KaitenOptions
{
    public const string SectionName = "Throne:TaskTrackers:Kaiten";

    /// <summary>Client-side rate limit. <c>0</c> or negative disables throttling.</summary>
    public double RequestsPerSecond { get; set; } = 5;

    /// <summary>Total attempts including the first; <c>1</c> disables retries.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff on a retryable status without a Retry-After hint.</summary>
    public int RetryBaseDelayMilliseconds { get; set; } = 500;

    /// <summary>Upper bound for the computed backoff delay.</summary>
    public int RetryMaxDelayMilliseconds { get; set; } = 5000;

    /// <summary>Per-attempt HTTP timeout applied to the pooled <see cref="System.Net.Http.HttpClient"/>.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
