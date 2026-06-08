using Microsoft.Extensions.Options;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal sealed class GlabPullRequestActions(GlabCliInvoker glab, IOptions<GitLabSettings> settings)
{
    public async Task<PullRequestSnapshot?> GetAsync(string owner, string repo, int number, CancellationToken ct)
    {
        var host = ReadHost();
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
        var host = ReadHost();
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

    private string ReadHost()
    {
        var host = settings.Value.Host?.Trim();
        return string.IsNullOrWhiteSpace(host)
            ? throw new GitProviderException(GitProviderErrorKind.CliFailure, "Throne:GitLab:Host is not configured.")
            : host;
    }
}
