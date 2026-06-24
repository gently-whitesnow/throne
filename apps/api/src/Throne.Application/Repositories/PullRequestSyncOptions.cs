namespace Throne.Application.Repositories;

/// <summary>
/// Bind target for the <c>Throne:Pr</c> configuration section (ADR-0024 § 6).
/// A 404 from upstream takes a different path (binding → broken, see workflow).
/// Upper bound on the backoff caps the wait so a long-suspended provider eventually
/// retries instead of silently giving up.
/// </summary>
public sealed class PullRequestSyncOptions
{
    public const string SectionName = "Throne:Pr";

    public int PollIntervalSeconds { get; set; } = 120;

    public int BackoffInitialSeconds { get; set; } = 30;

    public int BackoffMaxSeconds { get; set; } = 900;
}
