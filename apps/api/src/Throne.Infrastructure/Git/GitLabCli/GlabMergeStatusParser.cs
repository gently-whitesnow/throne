using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Parses <c>GET /projects/:id/merge_requests/:iid</c> into a provider-neutral
/// <see cref="PullRequestMergeStatus"/>. Prefers <c>detailed_merge_status</c>
/// (GitLab 15.6+) and falls back to the deprecated <c>merge_status</c>; checks
/// state comes from <c>head_pipeline.status</c>.
/// </summary>
internal static class GlabMergeStatusParser
{
    public static PullRequestMergeStatus Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new PullRequestMergeStatus(
            Mergeability: MapMergeability(root),
            Checks: MapChecks(root),
            HtmlUrl: GlabJson.String(root, "web_url"));
    }

    private static PullRequestMergeability MapMergeability(JsonElement root)
    {
        var detailed = GlabJson.String(root, "detailed_merge_status");
        if (!string.IsNullOrEmpty(detailed))
        {
            return detailed.ToLowerInvariant() switch
            {
                "mergeable" => PullRequestMergeability.Mergeable,
                "conflict" => PullRequestMergeability.Conflicting,
                "need_rebase" => PullRequestMergeability.Behind,
                "checking" or "unchecked" or "preparing" or "approvals_syncing" =>
                    PullRequestMergeability.Checking,
                "not_open" => PullRequestMergeability.Unknown,
                _ => PullRequestMergeability.Blocked,
            };
        }
        return GlabJson.String(root, "merge_status")?.ToLowerInvariant() switch
        {
            "can_be_merged" => PullRequestMergeability.Mergeable,
            "cannot_be_merged" => PullRequestMergeability.Conflicting,
            "checking" or "unchecked" => PullRequestMergeability.Checking,
            _ => PullRequestMergeability.Unknown,
        };
    }

    private static PullRequestChecksState MapChecks(JsonElement root)
    {
        if (!root.TryGetProperty("head_pipeline", out var pipeline) || pipeline.ValueKind != JsonValueKind.Object)
        {
            return PullRequestChecksState.None;
        }
        return GlabJson.String(pipeline, "status")?.ToLowerInvariant() switch
        {
            null or "" => PullRequestChecksState.None,
            "success" or "skipped" or "manual" => PullRequestChecksState.Passing,
            "failed" or "canceled" or "cancelled" => PullRequestChecksState.Failing,
            "running" or "pending" or "created" or "scheduled" or "preparing" or "waiting_for_resource" =>
                PullRequestChecksState.Pending,
            _ => PullRequestChecksState.Unknown,
        };
    }
}
