namespace Throne.Application.Git;

/// <summary>
/// Typed snapshot of a pull request observed via an <see cref="IGitProvider"/>.
/// Lifecycle state decides whether to keep polling (open) or stop
/// (closed/merged) — see ADR-0024 § 4.
/// </summary>
/// <param name="Number">PR number within the repository.</param>
/// <param name="State">Lifecycle state, one of <c>PullRequestStateNames</c> values.</param>
/// <param name="Title">Pull request title.</param>
/// <param name="HtmlUrl">Browser-facing URL of the pull request.</param>
/// <param name="Body">Pull request description (Markdown), when present.</param>
/// <param name="AuthorLogin">Provider login of the PR author.</param>
/// <param name="AuthorAvatarUrl">Avatar URL of the PR author, when present.</param>
/// <param name="HeadRef">Source branch the PR merges from.</param>
/// <param name="BaseRef">Target branch the PR merges into.</param>
public sealed record PullRequestSnapshot(
    int Number,
    string State,
    string? Title = null,
    string? HtmlUrl = null,
    string? Body = null,
    string? AuthorLogin = null,
    string? AuthorAvatarUrl = null,
    string? HeadRef = null,
    string? BaseRef = null);
