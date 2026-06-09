using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Slice C merge surface for GitHub PRs. Reads mergeability via
/// <c>gh pr view --json</c> and merges via the <c>gh pr merge</c> porcelain so
/// branch-protection / merge-queue rules are enforced by the CLI exactly as a
/// human merge would be.
/// </summary>
internal sealed class GhMergeActions(GhCliInvoker gh)
{
    public async Task<PullRequestMergeStatus?> GetMergeStatusAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var args = new[]
        {
            "pr", "view", number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--repo", $"{owner}/{repo}",
            "--json", "mergeable,mergeStateStatus,statusCheckRollup,url",
        };
        var result = await gh.RunAsync(args, ct);
        if (result.IsSuccess)
        {
            return GhMergeStatusParser.Parse(result.StandardOutput);
        }
        if (IsNotFound(result.StandardError))
        {
            return null;
        }
        throw GhExceptions.FromExit($"pr view {owner}/{repo}#{number}", result);
    }

    public async Task<PullRequestMergeResult> MergeAsync(
        string owner,
        string repo,
        int number,
        MergePullRequestRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var args = new List<string>
        {
            "pr", "merge", number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--repo", $"{owner}/{repo}",
            StrategyFlag(request.Strategy),
        };
        if (request.DeleteBranch)
        {
            args.Add("--delete-branch");
        }
        var result = await gh.RunAsync(args, ct);
        if (result.IsSuccess)
        {
            return new PullRequestMergeResult(
                Merged: true,
                State: PullRequestStateNames.Merged,
                Message: GhErrorClassifier.OneLine(result.StandardOutput));
        }
        var stderr = result.StandardError ?? string.Empty;
        if (IsNotFound(stderr))
        {
            throw new GitProviderException(
                GitProviderErrorKind.NotFound,
                $"GitHub pull request #{number} not found ({owner}/{repo}).",
                detail: stderr);
        }
        if (IsMergeRefusal(stderr))
        {
            throw new GitProviderException(
                GitProviderErrorKind.MergeNotAllowed,
                $"GitHub refused to merge PR #{number}: {GhErrorClassifier.OneLine(stderr)}",
                detail: stderr);
        }
        throw GhExceptions.FromExit($"pr merge {owner}/{repo}#{number}", result);
    }

    private static string StrategyFlag(MergeStrategy strategy) => strategy switch
    {
        MergeStrategy.Squash => "--squash",
        MergeStrategy.Rebase => "--rebase",
        _ => "--merge",
    };

    private static bool IsNotFound(string? stderr) =>
        stderr is not null
        && (stderr.Contains("Could not resolve to a PullRequest", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("no pull requests found", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("HTTP 404", StringComparison.OrdinalIgnoreCase));

    // gh surfaces branch-protection / not-mergeable refusals as ordinary non-zero exits.
    // Sniff the stable phrases so they map to a 409 «go resolve on the provider» instead
    // of an opaque CLI failure.
    private static bool IsMergeRefusal(string stderr) =>
        stderr.Contains("not mergeable", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("not in the correct state", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("required status check", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("review is required", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("changes requested", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("Base branch was modified", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("protected branch", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("branch protection", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("merge conflict", StringComparison.OrdinalIgnoreCase)
        || stderr.Contains("is not allowed", StringComparison.OrdinalIgnoreCase);
}
