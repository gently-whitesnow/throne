namespace Throne.Application.Git;

/// <summary>
/// Typed projection of a single pull request returned by
/// <see cref="IGitProvider.ListPullRequestsAsync"/>. Mirrors
/// <c>GitPullRequestRefDto</c> in <c>specs/contracts/repositories/openapi.yaml</c>.
/// </summary>
/// <param name="Number">Pull request number (positive integer).</param>
/// <param name="Title">Pull request title as displayed in the UI list.</param>
/// <param name="HeadRef">Head branch ref of the pull request (source branch).</param>
/// <param name="State">Wire-format lifecycle state, one of <c>PullRequestStateNames</c>.</param>
public sealed record GitPullRequestRef(int Number, string Title, string HeadRef, string State);
