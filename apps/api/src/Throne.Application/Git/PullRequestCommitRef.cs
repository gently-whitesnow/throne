namespace Throne.Application.Git;

/// <summary>
/// One commit belonging to a pull/merge request, surfaced by the commits-list
/// endpoint of the review workspace. Backs the per-commit diff selector in the UI.
/// </summary>
/// <param name="Sha">Full commit SHA (40 hex chars).</param>
/// <param name="Message">Commit message — first line is title, rest is body.</param>
/// <param name="AuthorLogin">Author's provider login when available; falls back to display name.</param>
/// <param name="CommittedAt">UTC timestamp of the commit (authored date when committer date is absent).</param>
public sealed record PullRequestCommitRef(
    string Sha,
    string Message,
    string? AuthorLogin,
    DateTimeOffset CommittedAt);
