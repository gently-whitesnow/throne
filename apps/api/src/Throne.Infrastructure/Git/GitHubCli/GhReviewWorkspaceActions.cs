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

    public async Task<ReviewThreadState> ResolveReviewThreadAsync(
        string owner,
        string repo,
        int number,
        string threadId,
        bool resolved,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        var mutation = resolved ? "resolveReviewThread" : "unresolveReviewThread";
        var query =
            $"mutation($threadId:ID!){{{mutation}(input:{{threadId:$threadId}}){{thread{{id isResolved}}}}}}";
        var args = new[]
        {
            "api", "graphql",
            "-f", $"query={query}",
            "-f", $"threadId={threadId}",
        };
        var operation = $"api graphql {mutation}";
        var result = await gh.RunAsync(args, ct);
        if (!result.IsSuccess || GhReviewThreadMutationParser.HasErrors(result.StandardOutput))
        {
            throw GhReviewThreadFailure(operation, owner, repo, number, threadId, result);
        }
        return GhReviewThreadMutationParser.Parse(result.StandardOutput, mutation);
    }

    public async Task DeleteReviewCommentAsync(
        string owner,
        string repo,
        int number,
        string commentId,
        string? threadId,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commentId);
        // threadId is unused on GitHub — REST deletes a review comment by id alone.
        var path = $"/repos/{owner}/{repo}/pulls/comments/{commentId}";
        var args = new[] { "api", "-i", "-X", "DELETE", path };
        var operation = $"api DELETE {path}";
        var result = await gh.RunAsync(args, ct);
        var response = GhPrResponse.From(result);
        if (response.IsNotFound)
        {
            throw new GitProviderException(
                GitProviderErrorKind.NotFound,
                $"GitHub review comment '{commentId}' not found ({owner}/{repo} PR #{number}).",
                detail: response.Body);
        }
        GhPrResponseGuard.ThrowIfRateLimited(operation, response.Headers);
        GhPrResponseGuard.ThrowIfNotSuccess(operation, result, response.Status);
    }

    private static GitProviderException GhReviewThreadFailure(
        string operation,
        string owner,
        string repo,
        int number,
        string threadId,
        Throne.Application.Ports.ProcessRunResult result)
    {
        // graphql reports a missing thread as a NOT_FOUND error body even on exit 0;
        // sniff both stderr and the graphql error payload for that signal.
        var combined = $"{result.StandardError}\n{result.StandardOutput}";
        if (combined.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("Could not resolve", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase))
        {
            return new GitProviderException(
                GitProviderErrorKind.NotFound,
                $"GitHub review thread '{threadId}' not found ({owner}/{repo} PR #{number}).",
                detail: combined);
        }
        return GhExceptions.FromExit(operation, result);
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
