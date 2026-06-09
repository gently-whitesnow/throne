using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabPullRequestParser
{
    public static PullRequestSnapshot? Parse(string json, int requestedNumber)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var root = doc.RootElement;
        return new PullRequestSnapshot(
            Number: GlabJson.Int(root, "iid") ?? requestedNumber,
            State: GlabPullRequestState.Normalize(GlabJson.String(root, "state")),
            Title: GlabJson.String(root, "title"),
            HtmlUrl: GlabJson.String(root, "web_url"),
            Body: GlabJson.String(root, "description"),
            AuthorLogin: GlabJson.NestedString(root, "author", "username"),
            AuthorAvatarUrl: GlabJson.NestedString(root, "author", "avatar_url"),
            HeadRef: GlabJson.String(root, "source_branch"),
            BaseRef: GlabJson.String(root, "target_branch"));
    }
}
