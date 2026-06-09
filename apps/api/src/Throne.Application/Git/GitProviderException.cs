namespace Throne.Application.Git;

/// <summary>
/// Kinds of upstream failures an <see cref="IGitProvider"/> can surface. The
/// enum is intentionally small — callers only need to distinguish
/// «not authenticated», «upstream unreachable / network» and «404» to map them
/// to user-visible messages and clone-status transitions per ADR-0024 § 5/7.
/// </summary>
public enum GitProviderErrorKind
{
    /// <summary>Vendor CLI reports the user is not logged in or token is invalid.</summary>
    AuthFailed = 0,

    /// <summary>Upstream returned 404 (repository / PR not found).</summary>
    NotFound = 1,

    /// <summary>Network failure, timeout, DNS, TLS or any 5xx — caller may retry.</summary>
    NetworkError = 2,

    /// <summary>Vendor CLI is missing or returned an unrecognised failure.</summary>
    CliFailure = 3,

    /// <summary>
    /// Submit-review-comment anchor refers to a line/path that upstream does not
    /// accept against the supplied commit (typically «position cannot be mapped to
    /// the diff» or «outdated diff»). Caller should refetch the diff and retry.
    /// </summary>
    ReviewCommentAnchorInvalid = 4,
}

/// <summary>
/// Structured failure raised by <see cref="IGitProvider"/> implementations.
/// Wraps the vendor CLI stderr verbatim in <see cref="Detail"/> so callers can
/// render an actionable message without re-parsing CLI output.
/// </summary>
public sealed class GitProviderException : Exception
{
    public GitProviderException(GitProviderErrorKind kind, string message, string? detail = null)
        : base(message)
    {
        Kind = kind;
        Detail = detail;
    }

    public GitProviderException(GitProviderErrorKind kind, string message, string? detail, Exception innerException)
        : base(message, innerException)
    {
        Kind = kind;
        Detail = detail;
    }

    public GitProviderErrorKind Kind { get; }

    /// <summary>Verbatim CLI stderr (or other diagnostic) when available.</summary>
    public string? Detail { get; }
}
