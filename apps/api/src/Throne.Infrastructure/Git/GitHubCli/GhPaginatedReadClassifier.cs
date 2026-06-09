namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Outcome of a failed <c>gh api --paginate</c> read on the review-workspace
/// list endpoints (diff files, PR commits).
/// </summary>
internal enum GhReadFailureKind
{
    /// <summary>404 — the resource is gone; callers surface this as <c>null</c>.</summary>
    NotFound,

    /// <summary>Primary or secondary API rate limit — retryable network error.</summary>
    RateLimited,

    /// <summary>Anything else; falls through to the generic exit-code mapping.</summary>
    Other,
}

/// <summary>
/// Classifies <c>gh</c> stderr for the two paginated list reads that run without
/// <c>-i</c> (so header-based 404 / rate-limit detection from
/// <see cref="GhPrResponse"/> / <see cref="GhPrResponseGuard"/> is unavailable).
/// Mirrors the stderr-sniffing convention already used for glab.
/// </summary>
internal static class GhPaginatedReadClassifier
{
    public static GhReadFailureKind Classify(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return GhReadFailureKind.Other;
        }

        // gh reports a rate limit as "HTTP 403: API rate limit exceeded" (and a
        // secondary limit as "You have exceeded a secondary rate limit"). The
        // rate-limit signal must win over the generic 403, so it is checked
        // before 404 — otherwise it would silently land in CliFailure/AuthFailed.
        if (stderr.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            return GhReadFailureKind.RateLimited;
        }

        if (stderr.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            return GhReadFailureKind.NotFound;
        }

        return GhReadFailureKind.Other;
    }
}
