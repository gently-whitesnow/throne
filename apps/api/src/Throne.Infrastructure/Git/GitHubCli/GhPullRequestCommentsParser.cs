using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses the JSON body of
/// <c>GET /repos/{owner}/{repo}/pulls/{number}/comments</c> — the review-comments
/// feed only (not issue-comments).
/// </summary>
internal static class GhPullRequestCommentsParser
{
    public static IReadOnlyList<PullRequestComment> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PullRequestComment>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("gh pulls/comments returned non-array JSON payload.");
        }

        var result = new List<PullRequestComment>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var comment = GhPullRequestCommentProjector.Project(item);
            if (comment is not null)
            {
                result.Add(comment);
            }
        }

        return result;
    }
}
