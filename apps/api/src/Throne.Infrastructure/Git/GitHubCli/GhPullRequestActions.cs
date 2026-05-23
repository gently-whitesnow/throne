using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Pull-request operations shelled out via <c>gh api -i</c> (T-07). Sits
/// alongside <see cref="GhRepoActions"/> / <see cref="GhAuthProbe"/> so the
/// <see cref="GitHubCliProvider"/> façade stays trivial. The conditional-GET
/// decision tree (304 / 404 / rate-limit / 2xx) lives in
/// <see cref="GhPrResponse"/> so this orchestration class stays inside the
/// CA1502 cyclomatic budget.
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

        var operation = $"api repos/{owner}/{repo}/pulls/{number}/comments";
        var result = await gh.RunAsync(GhPrCommands.ListReviewComments(owner, repo, number, etag), ct);
        var response = GhPrResponse.From(result);

        if (response.IsNotModified)
        {
            return new PullRequestCommentsPage.NotModified();
        }

        if (response.IsNotFound)
        {
            return null;
        }

        GhPrResponseGuard.ThrowIfRateLimited(operation, response.Headers);
        GhPrResponseGuard.ThrowIfNotSuccess(operation, result, response.Status);

        var comments = GhPullRequestCommentsParser.Parse(response.Body);
        return new PullRequestCommentsPage.Fresh(comments, response.Etag);
    }
}
