using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Slice C merge surface for GitLab MRs. Reads mergeability via <c>glab api</c>
/// (<c>detailed_merge_status</c> + head pipeline) and merges via the
/// <c>glab mr merge</c> porcelain so the instance's merge-method and approval
/// rules are honoured.
/// </summary>
internal sealed class GlabMergeActions(GlabCliInvoker glab, IGitLabHostProvider hostProvider)
{
    public async Task<PullRequestMergeStatus?> GetMergeStatusAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var host = await hostProvider.GetHostAsync(ct);
        var project = GlabProjectPath.ApiId(owner, repo);
        var result = await glab.RunAsync(
            ["api", $"projects/{project}/merge_requests/{number}"],
            GlabEnvironment.ForHost(host),
            ct);
        if (result.IsSuccess)
        {
            return GlabMergeStatusParser.Parse(result.StandardOutput);
        }
        return GlabErrorClassifier.Classify(result.StandardError) == GitProviderErrorKind.NotFound
            ? null
            : throw GlabExceptions.FromExit($"api projects/{project}/merge_requests/{number}", result);
    }

    public async Task<PullRequestMergeResult> MergeAsync(
        string owner,
        string repo,
        int number,
        MergePullRequestRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var host = await hostProvider.GetHostAsync(ct);
        var args = new List<string>
        {
            "mr", "merge", number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-R", GlabProjectPath.FullPath(owner, repo),
            "--yes",
        };
        AppendStrategy(args, request.Strategy);
        if (request.DeleteBranch)
        {
            args.Add("--remove-source-branch");
        }
        var result = await glab.RunAsync(args, GlabEnvironment.ForHost(host), ct);
        if (result.IsSuccess)
        {
            return new PullRequestMergeResult(
                Merged: true,
                State: PullRequestStateNames.Merged,
                Message: GlabErrorClassifier.OneLine(result.StandardOutput));
        }
        var stderr = result.StandardError ?? string.Empty;
        if (GlabErrorClassifier.Classify(stderr) == GitProviderErrorKind.NotFound)
        {
            throw new GitProviderException(
                GitProviderErrorKind.NotFound,
                $"GitLab merge request !{number} not found ({owner}/{repo}).",
                detail: stderr);
        }
        if (IsMergeRefusal(stderr))
        {
            throw new GitProviderException(
                GitProviderErrorKind.MergeNotAllowed,
                $"GitLab refused to merge MR !{number}: {GlabErrorClassifier.OneLine(stderr)}",
                detail: stderr);
        }
        throw GlabExceptions.FromExit($"mr merge {owner}/{repo}!{number}", result);
    }

    private static void AppendStrategy(List<string> args, MergeStrategy strategy)
    {
        switch (strategy)
        {
            case MergeStrategy.Squash:
                args.Add("--squash");
                break;
            case MergeStrategy.Rebase:
                args.Add("--rebase");
                break;
                // Default merge-commit method needs no flag.
        }
    }

    // GitLab returns 405/406/409/422 for unmet merge conditions; the CLI prints those
    // as non-zero exits with the reason in stderr. Map them to «go resolve on GitLab».
    private static bool IsMergeRefusal(string stderr) =>
        stderr.Contains("405", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("406", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("409", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("422", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("not allowed", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("cannot be merged", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("conflict", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("pipeline", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("approv", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("unresolved", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("draft", StringComparison.OrdinalIgnoreCase);

}
