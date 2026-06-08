namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Strategy for one of the two GitHub comment feeds — discussion-thread
/// (<c>/issues/{n}/comments</c>) or inline review (<c>/pulls/{n}/comments</c>).
/// </summary>
internal abstract class GhCommentEndpoint
{
    public static readonly GhCommentEndpoint Issues = new IssuesEndpoint();
    public static readonly GhCommentEndpoint Review = new ReviewEndpoint();

    public abstract string[] BuildArgs(string owner, string repo, int number, string? etag);

    public abstract string OperationLabel(string owner, string repo, int number);

    private sealed class IssuesEndpoint : GhCommentEndpoint
    {
        public override string[] BuildArgs(string owner, string repo, int number, string? etag) =>
            GhPrCommands.ListIssueComments(owner, repo, number, etag);

        public override string OperationLabel(string owner, string repo, int number) =>
            $"api repos/{owner}/{repo}/issues/{number}/comments";
    }

    private sealed class ReviewEndpoint : GhCommentEndpoint
    {
        public override string[] BuildArgs(string owner, string repo, int number, string? etag) =>
            GhPrCommands.ListReviewComments(owner, repo, number, etag);

        public override string OperationLabel(string owner, string repo, int number) =>
            $"api repos/{owner}/{repo}/pulls/{number}/comments";
    }
}
