namespace Throne.Application.Git;

/// <summary>How a pull/merge request is integrated — see the OpenAPI <c>MergeStrategy</c> enum.</summary>
public enum MergeStrategy
{
    Merge = 0,
    Squash = 1,
    Rebase = 2,
}

/// <summary>
/// Provider-neutral merge request: the chosen <see cref="MergeStrategy"/> and whether
/// to delete the source branch afterwards. Adapters translate it into
/// <c>gh pr merge</c> / <c>glab mr merge</c> flags.
/// </summary>
public sealed record MergePullRequestRequest(MergeStrategy Strategy, bool DeleteBranch);

/// <summary>
/// Outcome of a successful merge. The provider refusing the merge is signalled by a
/// <see cref="GitProviderException"/> with <see cref="GitProviderErrorKind.MergeNotAllowed"/>,
/// not by <see cref="Merged"/> being false.
/// </summary>
/// <param name="Merged">True when the provider reported the request as merged.</param>
/// <param name="State">Resulting lifecycle state, one of <c>PullRequestStateNames</c> values.</param>
/// <param name="Message">Provider's one-line summary of the outcome, when available.</param>
public sealed record PullRequestMergeResult(bool Merged, string State, string? Message);
