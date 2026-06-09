using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Parses GitLab's <c>POST /merge_requests/:iid/discussions</c> response into a
/// provider-neutral <see cref="SubmittedReviewComment"/>. GitLab returns a
/// Discussion with a <c>notes</c> array — the first note is the comment the
/// caller just submitted.
/// </summary>
internal static class GlabSubmittedCommentParser
{
    public static SubmittedReviewComment Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("glab discussion submit returned non-object JSON payload.");
        }
        if (!doc.RootElement.TryGetProperty("notes", out var notes) || notes.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("glab discussion submit response has no 'notes' array.");
        }
        foreach (var note in notes.EnumerateArray())
        {
            var comment = TryParseUserNote(note);
            if (comment is not null)
            {
                return comment;
            }
        }
        throw new FormatException("glab discussion submit response has no user note.");
    }

    private static SubmittedReviewComment? TryParseUserNote(JsonElement note)
    {
        if (note.ValueKind != JsonValueKind.Object || GlabJson.Bool(note, "system"))
        {
            return null;
        }
        var id = ReadId(note);
        if (id is null)
        {
            return null;
        }
        return new SubmittedReviewComment(
            Id: id,
            AuthorLogin: GlabJson.NestedString(note, "author", "username") ?? string.Empty,
            Body: GlabJson.String(note, "body") ?? string.Empty,
            CreatedAt: GlabJson.Timestamp(note, "created_at") ?? DateTimeOffset.UtcNow,
            HtmlUrl: GlabJson.String(note, "html_url"));
    }

    private static string? ReadId(JsonElement note) =>
        note.TryGetProperty("id", out var value) ? value.GetRawText().Trim('"') : null;
}
