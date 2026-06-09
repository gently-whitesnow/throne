using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Parses GitLab's commits-of-MR JSON arrays into
/// <see cref="PullRequestCommitRef"/>. Used by both
/// <c>GET projects/:id/merge_requests/:iid/commits</c> and
/// <c>GET projects/:id/repository/commits/:sha</c> (single-element shape).
/// </summary>
internal static class GlabPullRequestCommitsParser
{
    public static IReadOnlyList<PullRequestCommitRef> Parse(string json)
    {
        var result = new List<PullRequestCommitRef>();
        GlabPaginatedJson.ForEachElement(json, element =>
        {
            var commit = ProjectCommit(element);
            if (commit is not null)
            {
                result.Add(commit);
            }
        });
        return result;
    }

    public static PullRequestCommitRef? ProjectCommit(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var sha = GlabJson.String(element, "id");
        if (string.IsNullOrEmpty(sha))
        {
            return null;
        }
        var createdAt = GlabJson.Timestamp(element, "committed_date")
            ?? GlabJson.Timestamp(element, "authored_date")
            ?? GlabJson.Timestamp(element, "created_at")
            ?? DateTimeOffset.MinValue;
        return new PullRequestCommitRef(
            Sha: sha,
            Message: GlabJson.String(element, "message") ?? string.Empty,
            AuthorLogin: GlabJson.String(element, "author_name") ?? GlabJson.String(element, "author_email"),
            CommittedAt: createdAt);
    }
}
