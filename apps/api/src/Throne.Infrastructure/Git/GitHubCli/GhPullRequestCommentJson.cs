using System.Text.Json;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// JSON readers for a single GitHub review-comment object.
/// </summary>
internal static class GhPullRequestCommentJson
{
    public static string? ReadId(JsonElement item) =>
        item.TryGetProperty("id", out var value) ? IdToString(value) : null;

    public static DateTimeOffset? ReadTimestamp(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.TryGetDateTimeOffset(out var parsed) ? parsed : null;
    }

    private static string? IdToString(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.String => value.GetString(),
        _ => null,
    };
}
