using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Pull-request operations shelled out via <c>gh api -i</c>. The conditional-GET
/// decision tree (304 / 404 / rate-limit / 2xx) lives in <see cref="GhPrResponse"/>.
///
/// Comments coverage spans two GitHub endpoints — discussion-thread comments
/// (<c>/issues/{n}/comments</c>) and inline review comments
/// (<c>/pulls/{n}/comments</c>). They are fetched in parallel and merged into
/// a single provider-neutral list; each side keeps its own ETag inside the
/// composite stored on the binding.
/// </summary>
internal sealed class GhPullRequestActions(GhCliInvoker gh)
{
    public async Task<PullRequestSnapshot?> GetAsync(string owner, string repo, int number, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);

        var operation = $"api repos/{owner}/{repo}/pulls/{number}";
        var result = await gh.RunAsync(GhPrCommands.GetPullRequest(owner, repo, number), ct);
        var response = GhPrResponse.From(result);

        if (response.IsNotFound)
        {
            return null;
        }

        GhPrResponseGuard.ThrowIfRateLimited(operation, response.Headers);
        GhPrResponseGuard.ThrowIfNotSuccess(operation, result, response.Status);

        return GhPullRequestParser.Parse(response.Body, number);
    }

    public async Task<PullRequestCommentsPage?> ListReviewCommentsAsync(
        string owner,
        string repo,
        int number,
        string? etag,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);

        var composite = GhPullRequestCommentsEtag.Decode(etag);
        var issuesTask = FetchEndpointAsync(owner, repo, number, GhCommentEndpoint.Issues, composite.Issues, ct);
        var reviewTask = FetchEndpointAsync(owner, repo, number, GhCommentEndpoint.Review, composite.Review, ct);
        var issues = await issuesTask;
        var review = await reviewTask;

        if (issues is null || review is null)
        {
            return null;
        }

        if (issues.NotModified && review.NotModified)
        {
            return new PullRequestCommentsPage.NotModified();
        }

        var refreshedIssues = await EnsureBodiesAsync(owner, repo, number, GhCommentEndpoint.Issues, issues, ct);
        var refreshedReview = await EnsureBodiesAsync(owner, repo, number, GhCommentEndpoint.Review, review, ct);
        if (refreshedIssues is null || refreshedReview is null)
        {
            return null;
        }

        var combined = Merge(refreshedIssues.Comments, refreshedReview.Comments);
        var compositeEtag = GhPullRequestCommentsEtag.Encode(refreshedIssues.Etag, refreshedReview.Etag);
        return new PullRequestCommentsPage.Fresh(combined, compositeEtag);
    }

    private async Task<EndpointResult?> EnsureBodiesAsync(
        string owner,
        string repo,
        int number,
        GhCommentEndpoint endpoint,
        EndpointResult side,
        CancellationToken ct)
    {
        if (!side.NotModified)
        {
            return side;
        }

        // The other side is fresh — we need full bodies for both to emit per-comment events
        // and to return a complete feed to UI callers.
        return await FetchEndpointAsync(owner, repo, number, endpoint, etag: null, ct);
    }

    private async Task<EndpointResult?> FetchEndpointAsync(
        string owner,
        string repo,
        int number,
        GhCommentEndpoint endpoint,
        string? etag,
        CancellationToken ct)
    {
        var operation = endpoint.OperationLabel(owner, repo, number);
        var result = await gh.RunAsync(endpoint.BuildArgs(owner, repo, number, etag), ct);
        var response = GhPrResponse.From(result);

        if (response.IsNotModified)
        {
            return EndpointResult.NotModifiedAt(etag);
        }

        if (response.IsNotFound)
        {
            return null;
        }

        GhPrResponseGuard.ThrowIfRateLimited(operation, response.Headers);
        GhPrResponseGuard.ThrowIfNotSuccess(operation, result, response.Status);

        var comments = GhPullRequestCommentsParser.Parse(response.Body);
        return EndpointResult.Fresh(comments, response.Etag);
    }

    private static List<PullRequestComment> Merge(
        IReadOnlyList<PullRequestComment> issues,
        IReadOnlyList<PullRequestComment> review)
    {
        var combined = new List<PullRequestComment>(issues.Count + review.Count);
        combined.AddRange(issues);
        combined.AddRange(review);
        combined.Sort(static (a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        return combined;
    }

    private sealed record EndpointResult(bool NotModified, IReadOnlyList<PullRequestComment> Comments, string? Etag)
    {
        public static EndpointResult Fresh(IReadOnlyList<PullRequestComment> comments, string? etag) =>
            new(NotModified: false, comments, etag);

        public static EndpointResult NotModifiedAt(string? etag) =>
            new(NotModified: true, Array.Empty<PullRequestComment>(), etag);
    }
}
