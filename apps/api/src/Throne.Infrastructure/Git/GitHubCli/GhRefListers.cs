namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Aggregator for the two upstream-ref listers (<see cref="GhBranchLister"/> and
/// <see cref="GhPullRequestLister"/>) used by the bind-modal typeahead. Wrapping
/// them keeps <see cref="GitHubCliProvider"/>'s primary constructor inside the
/// repository's CTOR_DEPS ≤5 budget — both listers serve the same UX surface
/// (live ref suggestions for an already-resolved repository).
/// </summary>
internal sealed class GhRefListers(GhBranchLister branches, GhPullRequestLister pullRequests)
{
    public GhBranchLister Branches { get; } = branches;

    public GhPullRequestLister PullRequests { get; } = pullRequests;
}
