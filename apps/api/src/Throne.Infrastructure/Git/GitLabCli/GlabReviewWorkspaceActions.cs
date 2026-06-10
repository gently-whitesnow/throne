using Microsoft.Extensions.Options;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Slice 4A review-workspace surface for GitLab MRs backed by raw
/// <c>glab api</c>. Provider-neutral I/O: anchors and SHAs round-trip back via
/// <see cref="SubmitReviewCommentAsync"/> from
/// <see cref="GetPullRequestDiffAsync"/> output.
/// </summary>
internal sealed partial class GlabReviewWorkspaceActions(GlabCliInvoker glab, IOptions<GitLabSettings> settings)
{
    public async Task<PullRequestDiff?> GetPullRequestDiffAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var host = ReadHost();
        var project = GlabProjectPath.ApiId(owner, repo);
        var mrJson = await ApiAsync(host, $"projects/{project}/merge_requests/{number}", ct);
        if (mrJson is null)
        {
            return null;
        }
        var refs = GlabMrDiffRefsParser.Parse(mrJson)
            ?? throw new FormatException("glab MR response missing diff_refs.");
        var diffsJson = await ApiAsync(host, $"projects/{project}/merge_requests/{number}/diffs", ct, paginate: true);
        if (diffsJson is null)
        {
            return null;
        }
        return new PullRequestDiff(
            BaseSha: refs.BaseSha,
            HeadSha: refs.HeadSha,
            StartSha: refs.StartSha,
            Files: GlabPullRequestDiffParser.Parse(diffsJson));
    }

    public async Task<PullRequestDiff?> GetCommitDiffAsync(
        string owner,
        string repo,
        string commitSha,
        CancellationToken ct)
    {
        var host = ReadHost();
        var project = GlabProjectPath.ApiId(owner, repo);
        var commitJson = await ApiAsync(host, $"projects/{project}/repository/commits/{commitSha}", ct);
        if (commitJson is null)
        {
            return null;
        }
        var parentSha = GlabCommitDiffParser.ReadParentSha(commitJson) ?? commitSha;
        var diffsJson = await ApiAsync(host, $"projects/{project}/repository/commits/{commitSha}/diff", ct);
        if (diffsJson is null)
        {
            return null;
        }
        return new PullRequestDiff(
            BaseSha: parentSha,
            HeadSha: commitSha,
            StartSha: parentSha,
            Files: GlabPullRequestDiffParser.Parse(diffsJson));
    }

    public async Task<IReadOnlyList<PullRequestCommitRef>?> ListCommitsAsync(
        string owner,
        string repo,
        int number,
        CancellationToken ct)
    {
        var host = ReadHost();
        var project = GlabProjectPath.ApiId(owner, repo);
        var json = await ApiAsync(host, $"projects/{project}/merge_requests/{number}/commits", ct, paginate: true);
        return json is null ? null : GlabPullRequestCommitsParser.Parse(json);
    }

    public async Task<SubmittedReviewComment> SubmitReviewCommentAsync(
        string owner,
        string repo,
        int number,
        SubmitReviewCommentRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var host = ReadHost();
        var project = GlabProjectPath.ApiId(owner, repo);
        var apiPath = $"projects/{project}/merge_requests/{number}/discussions";
        var args = BuildSubmitArgs(apiPath, request);
        var operation = $"api POST {apiPath}";
        var result = await glab.RunAsync(args, GlabEnvironment.ForHost(host), ct);
        if (!result.IsSuccess)
        {
            ThrowSubmitException(operation, result);
        }
        return GlabSubmittedCommentParser.Parse(result.StandardOutput);
    }

    private static List<string> BuildSubmitArgs(string apiPath, SubmitReviewCommentRequest request)
    {
        if (request.OldLine is null && request.NewLine is null)
        {
            throw new ArgumentException(
                "SubmitReviewCommentRequest must carry at least one of OldLine / NewLine.",
                nameof(request));
        }
        var oldPath = request.PreviousPath ?? request.Path;
        var args = new List<string>
        {
            "api",
            "-X", "POST",
            "-f", $"body={request.Body}",
            "-f", "position[position_type]=text",
            "-f", $"position[base_sha]={request.BaseSha}",
            "-f", $"position[head_sha]={request.CommitSha}",
            "-f", $"position[start_sha]={request.StartSha}",
            "-f", $"position[new_path]={request.Path}",
            "-f", $"position[old_path]={oldPath}",
        };
        // GitLab quirk: for unchanged context lines a discussion anchor requires
        // BOTH position[new_line] AND position[old_line]; supplying only one
        // creates a non-positioned discussion (бьёт user-visible "не прикрепляется").
        if (request.NewLine is { } newLine)
        {
            args.Add("-f");
            args.Add($"position[new_line]={newLine}");
        }
        if (request.OldLine is { } oldLine)
        {
            args.Add("-f");
            args.Add($"position[old_line]={oldLine}");
        }
        args.Add(apiPath);
        return args;
    }

    private static void ThrowSubmitException(string operation, Throne.Application.Ports.ProcessRunResult result)
    {
        var stderr = result.StandardError ?? string.Empty;
        if (stderr.Contains("400", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("Position is incorrect", StringComparison.OrdinalIgnoreCase)
            || stderr.Contains("line_code", StringComparison.OrdinalIgnoreCase))
        {
            throw new GitProviderException(
                GitProviderErrorKind.ReviewCommentAnchorInvalid,
                $"GitLab rejected the review comment anchor: {GlabErrorClassifier.OneLine(stderr)}",
                detail: stderr);
        }
        throw GlabExceptions.FromExit(operation, result);
    }

    private async Task<string?> ApiAsync(string host, string apiPath, CancellationToken ct, bool paginate = false)
    {
        var args = paginate
            ? new[] { "api", apiPath, "--paginate" }
            : new[] { "api", apiPath };
        var result = await glab.RunAsync(args, GlabEnvironment.ForHost(host), ct);
        if (result.IsSuccess)
        {
            return result.StandardOutput;
        }
        if (GlabErrorClassifier.Classify(result.StandardError) == GitProviderErrorKind.NotFound)
        {
            return null;
        }
        throw GlabExceptions.FromExit($"api {apiPath}", result);
    }

    private string ReadHost()
    {
        var host = settings.Value.Host?.Trim();
        return string.IsNullOrWhiteSpace(host)
            ? throw new GitProviderException(GitProviderErrorKind.CliFailure, "Throne:GitLab:Host is not configured.")
            : host;
    }
}
