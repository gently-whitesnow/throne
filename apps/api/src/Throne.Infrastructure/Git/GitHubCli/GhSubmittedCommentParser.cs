using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses the JSON body of a successful
/// <c>POST /repos/{o}/{r}/pulls/{n}/comments</c> into a
/// <see cref="SubmittedReviewComment"/> that the UI can render immediately.
/// </summary>
internal static class GhSubmittedCommentParser
{
    public static SubmittedReviewComment Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("gh review-comment submit returned non-object JSON payload.");
        }
        var root = doc.RootElement;
        var id = ReadId(root)
            ?? throw new FormatException("gh review-comment submit response has no 'id'.");
        var createdAtRaw = GhJson.String(root, "created_at");
        var createdAt = createdAtRaw is not null && DateTimeOffset.TryParse(createdAtRaw, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;
        return new SubmittedReviewComment(
            Id: id,
            AuthorLogin: GhJson.NestedString(root, "user", "login") ?? string.Empty,
            Body: GhJson.String(root, "body") ?? string.Empty,
            CreatedAt: createdAt,
            HtmlUrl: GhJson.String(root, "html_url"));
    }

    private static string? ReadId(JsonElement root) =>
        root.TryGetProperty("id", out var idValue)
            ? idValue.ValueKind switch
            {
                JsonValueKind.Number => idValue.GetRawText(),
                JsonValueKind.String => idValue.GetString(),
                _ => null,
            }
            : null;
}
