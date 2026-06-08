namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabRefListers(GlabBranchLister branches, GlabPullRequestLister pullRequests)
{
    public GlabBranchLister Branches { get; } = branches;

    public GlabPullRequestLister PullRequests { get; } = pullRequests;
}
