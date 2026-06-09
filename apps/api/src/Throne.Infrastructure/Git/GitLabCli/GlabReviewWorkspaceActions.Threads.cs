using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Slice B review-thread surface for GitLab MRs: resolve/reopen a discussion and
/// delete a single note. Kept in a partial alongside the diff/comments path.
/// </summary>
internal sealed partial class GlabReviewWorkspaceActions
{
    public async Task<ReviewThreadState> ResolveReviewThreadAsync(
        string owner,
        string repo,
        int number,
        string threadId,
        bool resolved,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(threadId);
        var host = ReadHost();
        var project = GlabProjectPath.ApiId(owner, repo);
        var flag = resolved ? "true" : "false";
        var apiPath = $"projects/{project}/merge_requests/{number}/discussions/{threadId}?resolved={flag}";
        var operation = $"api PUT {apiPath}";
        var result = await glab.RunAsync(["api", "-X", "PUT", apiPath], GlabEnvironment.ForHost(host), ct);
        if (!result.IsSuccess)
        {
            throw GlabExceptions.FromExit(operation, result);
        }
        return new ReviewThreadState(threadId, GlabResolvedDiscussionParser.ReadResolved(result.StandardOutput));
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
        if (string.IsNullOrWhiteSpace(threadId))
        {
            // GitLab keys a note delete by its discussion; without the thread id
            // there is nothing to address — the API layer renders this as 422.
            throw new GitProviderException(
                GitProviderErrorKind.ReviewCommentAnchorInvalid,
                "GitLab note delete requires a thread_id (discussion id).");
        }
        var host = ReadHost();
        var project = GlabProjectPath.ApiId(owner, repo);
        var apiPath =
            $"projects/{project}/merge_requests/{number}/discussions/{threadId}/notes/{commentId}";
        var operation = $"api DELETE {apiPath}";
        var result = await glab.RunAsync(["api", "-X", "DELETE", apiPath], GlabEnvironment.ForHost(host), ct);
        if (result.IsSuccess)
        {
            return;
        }
        if (GlabErrorClassifier.Classify(result.StandardError) == GitProviderErrorKind.NotFound)
        {
            throw new GitProviderException(
                GitProviderErrorKind.NotFound,
                $"GitLab note '{commentId}' not found in discussion '{threadId}' ({owner}/{repo} MR !{number}).",
                detail: result.StandardError);
        }
        throw GlabExceptions.FromExit(operation, result);
    }
}
