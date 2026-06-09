namespace Throne.Application.Git;

/// <summary>
/// Typed projection of a single PR review comment fetched through an
/// <see cref="IGitProvider"/>. Mirrors the <c>PullRequestCommentDto</c> in the
/// OpenAPI contract but lives in Application so the port stays free of
/// generated DTO types.
/// </summary>
/// <param name="Id">Upstream comment id (stringified for wire stability).</param>
/// <param name="AuthorLogin">Author's provider login.</param>
/// <param name="Body">Comment body (Markdown).</param>
/// <param name="CreatedAt">UTC timestamp of comment creation.</param>
/// <param name="AuthorAvatarUrl">Author avatar URL, when available.</param>
/// <param name="HtmlUrl">Browser-facing URL of the comment.</param>
/// <param name="Path">File path the review comment anchors to, when applicable.</param>
/// <param name="UpdatedAt">UTC timestamp of last edit, when available.</param>
/// <param name="Line">1-based line on <paramref name="Side"/> the comment anchors to; null without a diff anchor.</param>
/// <param name="Side">Diff side <paramref name="Line"/> refers to; null without a diff anchor.</param>
/// <param name="Resolved">Resolution state read from the provider; null when the comment is not part of a resolvable thread.</param>
/// <param name="ThreadId">Provider thread/discussion id used to resolve; null when no resolvable thread exists.</param>
public sealed record PullRequestComment(
    string Id,
    string AuthorLogin,
    string Body,
    DateTimeOffset CreatedAt,
    string? AuthorAvatarUrl = null,
    string? HtmlUrl = null,
    string? Path = null,
    DateTimeOffset? UpdatedAt = null,
    int? Line = null,
    ReviewCommentSide? Side = null,
    bool? Resolved = null,
    string? ThreadId = null);

/// <summary>
/// Result of <see cref="IGitProvider.ListPullRequestCommentsAsync"/>. Either a
/// fresh feed plus the new ETag, or <see cref="NotModified"/> when upstream
/// returned 304 for the conditional GET — see ADR-0024 § 4 (conditional GET
/// is what keeps the polling loop inside GitHub's rate limit).
/// </summary>
public abstract record PullRequestCommentsPage
{
    /// <summary>
    /// Upstream returned a 200 with a fresh comments feed.
    /// <paramref name="Etag"/> is what callers persist for the next conditional GET.
    /// </summary>
    public sealed record Fresh(IReadOnlyList<PullRequestComment> Comments, string? Etag) : PullRequestCommentsPage;

    /// <summary>
    /// Upstream returned 304 (nothing changed since the supplied ETag).
    /// Callers reuse their previously stored feed and skip fanout.
    /// </summary>
    public sealed record NotModified : PullRequestCommentsPage;
}
