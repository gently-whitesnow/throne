using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Per-item projector for a GitHub review-comment JSON object. Best-effort:
/// missing required fields drop the row rather than throwing — the polling
/// service prefers a partial feed over a poison message.
/// </summary>
internal static class GhPullRequestCommentProjector
{
    public static PullRequestComment? Project(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = GhPullRequestCommentJson.ReadId(item);
        var login = GhJson.NestedString(item, "user", "login");
        var createdAt = GhPullRequestCommentJson.ReadTimestamp(item, "created_at");
        if (id is null || login is null || createdAt is null)
        {
            return null;
        }

        return Build(item, id, login, createdAt.Value);
    }

    private static PullRequestComment Build(
        JsonElement item, string id, string login, DateTimeOffset createdAt) =>
        new(
            Id: id,
            AuthorLogin: login,
            Body: GhJson.String(item, "body") ?? string.Empty,
            CreatedAt: createdAt,
            AuthorAvatarUrl: GhJson.NestedString(item, "user", "avatar_url"),
            HtmlUrl: GhJson.String(item, "html_url"),
            Path: GhJson.String(item, "path"),
            UpdatedAt: GhPullRequestCommentJson.ReadTimestamp(item, "updated_at"));
}
