using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabPullRequestListParser
{
    public static IReadOnlyList<GitPullRequestRef> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GitPullRequestRef>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("glab api merge_requests returned non-array JSON payload.");
        }

        var result = new List<GitPullRequestRef>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var pr = TryProject(item);
            if (pr is not null)
            {
                result.Add(pr);
            }
        }

        return result;
    }

    private static GitPullRequestRef? TryProject(JsonElement item)
    {
        var iid = GlabJson.Int(item, "iid");
        var sourceBranch = GlabJson.String(item, "source_branch");
        if (iid is null || string.IsNullOrEmpty(sourceBranch))
        {
            return null;
        }

        return new GitPullRequestRef(
            iid.Value,
            GlabJson.String(item, "title") ?? string.Empty,
            sourceBranch,
            GlabPullRequestState.Normalize(GlabJson.String(item, "state")));
    }
}
