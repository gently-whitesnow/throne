using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Slice 4A review-workspace surface backed by <c>gh api -i</c>. Lives separately
/// from <see cref="GhPullRequestActions"/> so the snapshot/comments-feed path is
/// untouched.
/// </summary>
internal sealed class GhReviewWorkspaceActions(GhCliInvoker gh)
{
    public async Task<PullRequestDiff?> GetPullRequestDiffAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var prJson = await ApiAsync($"/repos/{owner}/{repo}/pulls/{number}", ct);
        if (prJson is null)
        {
            return null;
        }
        var shas = GhPullRequestShasParser.Parse(prJson)
            ?? throw new FormatException("gh pulls response missing base/head SHA.");
        var filesJson = await ApiAsync($"/repos/{owner}/{repo}/pulls/{number}/files?per_page=100", ct);
        if (filesJson is null)
        {
            return null;
        }
        var files = GhPullRequestDiffParser.Parse(filesJson);
        return new PullRequestDiff(
            BaseSha: shas.BaseSha,
            HeadSha: shas.HeadSha,
            StartSha: shas.BaseSha,
            Files: files);
    }

    public async Task<PullRequestDiff?> GetCommitDiffAsync(
        string owner,
        string repo,
        string commitSha,
        CancellationToken ct)
    {
        var json = await ApiAsync($"/repos/{owner}/{repo}/commits/{commitSha}", ct);
        return json is null ? null : GhCommitDiffParser.Parse(json, commitSha);
    }

    public async Task<IReadOnlyList<PullRequestCommitRef>?> ListCommitsAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var json = await ApiAsync($"/repos/{owner}/{repo}/pulls/{number}/commits?per_page=100", ct);
        return json is null ? null : GhPullRequestCommitsParser.Parse(json);
    }

    public async Task<SubmittedReviewComment> SubmitReviewCommentAsync(
        string owner,
        string repo,
        int number,
        SubmitReviewCommentRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var path = $"/repos/{owner}/{repo}/pulls/{number}/comments";
        var args = new List<string>
        {
            "api",
            "-i",
            "-X", "POST",
            "-f", $"body={request.Body}",
            "-f", $"commit_id={request.CommitSha}",
            "-f", $"path={request.Path}",
            "-F", $"line={request.Line}",
            "-f", $"side={(request.Side == ReviewCommentSide.Left ? "LEFT" : "RIGHT")}",
            path,
        };
        var operation = $"api POST {path}";
        var result = await gh.RunAsync(args, ct);
        var response = GhPrResponse.From(result);
        if (response.Status == 422)
        {
            throw new GitProviderException(
                GitProviderErrorKind.ReviewCommentAnchorInvalid,
                $"GitHub rejected the review comment anchor ({owner}/{repo} PR #{number}).",
                detail: response.Body);
        }
        GhPrResponseGuard.ThrowIfRateLimited(operation, response.Headers);
        GhPrResponseGuard.ThrowIfNotSuccess(operation, result, response.Status);
        return GhSubmittedCommentParser.Parse(response.Body);
    }

    private async Task<string?> ApiAsync(string apiPath, CancellationToken ct)
    {
        var args = new[] { "api", "-i", apiPath };
        var result = await gh.RunAsync(args, ct);
        var response = GhPrResponse.From(result);
        if (response.IsNotFound)
        {
            return null;
        }
        var operation = $"api {apiPath}";
        GhPrResponseGuard.ThrowIfRateLimited(operation, response.Headers);
        GhPrResponseGuard.ThrowIfNotSuccess(operation, result, response.Status);
        return response.Body;
    }
}
