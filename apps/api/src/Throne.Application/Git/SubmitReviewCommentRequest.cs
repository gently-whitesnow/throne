namespace Throne.Application.Git;

/// <summary>
/// Provider-neutral anchor + body for submitting an inline review comment back
/// to the upstream provider via <see cref="IGitProvider.SubmitReviewCommentAsync"/>.
/// Carries enough data to satisfy both mappings (Slice 4A scope):
/// <list type="bullet">
///   <item>GitHub review comment — <see cref="CommitSha"/> + <see cref="Path"/> + line + side.</item>
///   <item>GitLab MR discussion — <c>position{base/head/start_sha, old/new_path, old/new_line}</c>.</item>
/// </list>
/// Anchors are produced from the provider-derived <see cref="PullRequestDiff"/>, so
/// SHAs always round-trip back unchanged.
/// </summary>
/// <param name="Body">Comment body (Markdown).</param>
/// <param name="Path">New path of the file being commented on (post-change).</param>
/// <param name="PreviousPath">Old path of the file when renamed; falls back to <see cref="Path"/>.</param>
/// <param name="Side">Diff side the comment is displayed on (GitHub LEFT/RIGHT).</param>
/// <param name="OldLine">1-based line on the pre-change side; null for pure additions.</param>
/// <param name="NewLine">1-based line on the post-change side; null for pure deletions.</param>
/// <param name="CommitSha">Head commit SHA the anchor was computed against.</param>
/// <param name="BaseSha">Base SHA (GitLab MR discussion position).</param>
/// <param name="StartSha">Start SHA (GitLab MR discussion position).</param>
public sealed record SubmitReviewCommentRequest(
    string Body,
    string Path,
    string? PreviousPath,
    ReviewCommentSide Side,
    int? OldLine,
    int? NewLine,
    string CommitSha,
    string BaseSha,
    string StartSha);

/// <summary>
/// Side of the diff a comment is anchored to.
/// </summary>
public enum ReviewCommentSide
{
    /// <summary>New side (added/post-change lines). Maps to GitHub <c>RIGHT</c> and GitLab <c>new_line</c>.</summary>
    Right = 0,

    /// <summary>Old side (removed/pre-change lines). Maps to GitHub <c>LEFT</c> and GitLab <c>old_line</c>.</summary>
    Left = 1,
}

/// <summary>
/// Echoed back from the provider after a successful inline review comment submit.
/// Server is pointer-only: this is what the UI uses to display the just-sent comment
/// before the next manual refresh of the comments feed.
/// </summary>
public sealed record SubmittedReviewComment(
    string Id,
    string AuthorLogin,
    string Body,
    DateTimeOffset CreatedAt,
    string? HtmlUrl);

/// <summary>
/// Resolution state of a review thread as echoed back by the provider after a
/// resolve/reopen toggle. Throne keeps no local status — <see cref="Resolved"/>
/// is read straight from the provider response.
/// </summary>
public sealed record ReviewThreadState(string ThreadId, bool Resolved);
