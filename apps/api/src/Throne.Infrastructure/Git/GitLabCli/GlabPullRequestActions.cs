using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabPullRequestActions(GlabCliInvoker glab, IGitLabHostProvider hostProvider)
{
    public async Task<PullRequestSnapshot?> GetAsync(string owner, string repo, int number, CancellationToken ct)
    {
        var host = await hostProvider.GetHostAsync(ct);
        var project = GlabProjectPath.ApiId(owner, repo);
        var result = await glab.RunAsync(
            ["api", $"projects/{project}/merge_requests/{number}"],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            return GlabErrorClassifier.Classify(result.StandardError) == GitProviderErrorKind.NotFound
                ? null
                : throw GlabExceptions.FromExit($"api projects/{project}/merge_requests/{number}", result);
        }

        return GlabPullRequestParser.Parse(result.StandardOutput, number);
    }

    public async Task<PullRequestCommentsPage?> ListCommentsAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var host = await hostProvider.GetHostAsync(ct);
        var project = GlabProjectPath.ApiId(owner, repo);
        var result = await glab.RunAsync(
            ["api", $"projects/{project}/merge_requests/{number}/discussions", "--paginate"],
            GlabEnvironment.ForHost(host),
            ct);
        if (!result.IsSuccess)
        {
            return GlabErrorClassifier.Classify(result.StandardError) == GitProviderErrorKind.NotFound
                ? null
                : throw GlabExceptions.FromExit($"api projects/{project}/merge_requests/{number}/discussions", result);
        }

        var comments = GlabPullRequestCommentsParser.Parse(result.StandardOutput);
        return new PullRequestCommentsPage.Fresh(comments, Etag: null);
    }
}
