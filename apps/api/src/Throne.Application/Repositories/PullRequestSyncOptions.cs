namespace Throne.Application.Repositories;

/// <summary>
/// Bind target for the <c>Throne:Pr</c> configuration section (ADR-0024 § 6).
/// Drives the background <c>PullRequestSyncService</c> (T-10):
///
/// <list type="bullet">
///   <item><see cref="PollIntervalSeconds"/> — base tick between polls.</item>
///   <item><see cref="BackoffInitialSeconds"/> / <see cref="BackoffMaxSeconds"/> —
///         per-binding exponential backoff after a rate-limit or transient failure.
///         A 404 takes a different path (binding → broken, see workflow).</item>
/// </list>
///
/// Defaults are set per the parent slice brief (60s base tick); upper bound on the
/// backoff caps the wait so a long-suspended provider eventually retries instead of
/// silently giving up.
/// </summary>
public sealed class PullRequestSyncOptions
{
    public const string SectionName = "Throne:Pr";

    public int PollIntervalSeconds { get; set; } = 60;

    public int BackoffInitialSeconds { get; set; } = 30;

    public int BackoffMaxSeconds { get; set; } = 900;
}
