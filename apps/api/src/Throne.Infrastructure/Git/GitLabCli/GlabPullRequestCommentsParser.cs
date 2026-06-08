using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabPullRequestCommentsParser
{
    public static IReadOnlyList<PullRequestComment> Parse(string json)
    {
        var result = new List<PullRequestComment>();
        GlabPaginatedJson.ForEachElement(json, discussion => ProjectDiscussion(discussion, result));

        return result;
    }

    private static void ProjectDiscussion(JsonElement discussion, List<PullRequestComment> result)
    {
        if (!discussion.TryGetProperty("notes", out var notes) || notes.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var note in notes.EnumerateArray())
        {
            var comment = ProjectNote(note);
            if (comment is not null)
            {
                result.Add(comment);
            }
        }
    }

    private static PullRequestComment? ProjectNote(JsonElement note)
    {
        if (GlabJson.Bool(note, "system"))
        {
            return null;
        }

        var id = ReadId(note);
        var author = GlabJson.NestedString(note, "author", "username");
        var createdAt = GlabJson.Timestamp(note, "created_at");
        if (id is null || author is null || createdAt is null)
        {
            return null;
        }

        return new PullRequestComment(
            Id: id,
            AuthorLogin: author,
            Body: GlabJson.String(note, "body") ?? string.Empty,
            CreatedAt: createdAt.Value,
            AuthorAvatarUrl: GlabJson.NestedString(note, "author", "avatar_url"),
            HtmlUrl: GlabJson.String(note, "html_url"),
            Path: ReadPath(note),
            UpdatedAt: GlabJson.Timestamp(note, "updated_at"));
    }

    private static string? ReadId(JsonElement note) =>
        note.TryGetProperty("id", out var value) ? value.GetRawText().Trim('"') : null;

    private static string? ReadPath(JsonElement note)
    {
        if (!note.TryGetProperty("position", out var position) || position.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GlabJson.String(position, "new_path") ?? GlabJson.String(position, "old_path");
    }
}
