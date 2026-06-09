namespace Throne.Application.Git;

/// <summary>
/// Provider-derived diff of either a whole pull/merge request (<c>scope=request</c>)
/// or a single commit (<c>scope=commit</c>) — the source of truth for review-workspace
/// rendering and anchor data of inline comments. Both providers return the same shape:
/// a list of per-file unified-diff patches plus the SHAs needed to round-trip a review
/// comment back via <see cref="IGitProvider.SubmitReviewCommentAsync"/>.
/// </summary>
public sealed record PullRequestDiff(
    string BaseSha,
    string HeadSha,
    string StartSha,
    IReadOnlyList<PullRequestDiffFile> Files);

/// <summary>One file inside a <see cref="PullRequestDiff"/>.</summary>
/// <param name="Path">New path (post-change) inside the repo.</param>
/// <param name="PreviousPath">Old path when the file was renamed/copied, otherwise <see langword="null"/>.</param>
/// <param name="Status">Lifecycle of the file inside the diff.</param>
/// <param name="Patch">Raw unified diff hunks for this file (may be empty for binary/too-large files).</param>
public sealed record PullRequestDiffFile(
    string Path,
    string? PreviousPath,
    PullRequestDiffFileStatus Status,
    string Patch);

/// <summary>Status of a file inside a provider-derived diff.</summary>
public enum PullRequestDiffFileStatus
{
    Added = 0,
    Modified = 1,
    Removed = 2,
    Renamed = 3,
    Copied = 4,
}
