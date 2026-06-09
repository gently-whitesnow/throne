namespace Throne.Application.Git;

/// <summary>
/// Provider-neutral mergeability of a pull/merge request as observed via an
/// <see cref="IGitProvider"/>. Backs the merge control's enable/disable decision
/// and the «go to provider» escape hatch (Slice C). The two providers expose this
/// differently (GitHub <c>mergeable</c>/<c>mergeStateStatus</c>/<c>statusCheckRollup</c>,
/// GitLab <c>detailed_merge_status</c> + head pipeline) — adapters fold both into
/// this shape.
/// </summary>
/// <param name="Mergeability">Whether the request can be merged right now.</param>
/// <param name="Checks">Aggregate CI checks state on the request head.</param>
/// <param name="HtmlUrl">Browser-facing URL of the PR/MR page, when reported.</param>
public sealed record PullRequestMergeStatus(
    PullRequestMergeability Mergeability,
    PullRequestChecksState Checks,
    string? HtmlUrl);

/// <summary>Provider-neutral mergeability state — see the OpenAPI enum of the same name.</summary>
public enum PullRequestMergeability
{
    Mergeable = 0,
    Conflicting = 1,
    Blocked = 2,
    Behind = 3,
    Checking = 4,
    Unknown = 5,
}

/// <summary>Aggregate CI checks state on the request head.</summary>
public enum PullRequestChecksState
{
    None = 0,
    Pending = 1,
    Passing = 2,
    Failing = 3,
    Unknown = 4,
}
