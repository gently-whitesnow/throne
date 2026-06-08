using System.Text.Json;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Composite ETag stored on the binding so a single opaque string can drive
/// conditional GET against both GitHub comment feeds at once
/// (<c>/issues/{n}/comments</c> + <c>/pulls/{n}/comments</c>). Encoded as a
/// compact JSON object — values stay within the visible-ASCII subset accepted
/// by <c>ReviewCommentsEtagNormalizer</c>.
///
/// Legacy strings persisted before this change (a single review-comments ETag)
/// decode as <see cref="Review"/> only — issues-side falls through to a full
/// fetch on the next poll, after which a composite is written back.
/// </summary>
internal readonly record struct GhPullRequestCommentsEtag(string? Issues, string? Review)
{
    public static GhPullRequestCommentsEtag Decode(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return new GhPullRequestCommentsEtag(null, null);
        }

        if (stored[0] != '{')
        {
            return new GhPullRequestCommentsEtag(Issues: null, Review: stored);
        }

        try
        {
            using var doc = JsonDocument.Parse(stored);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new GhPullRequestCommentsEtag(null, null);
            }

            return new GhPullRequestCommentsEtag(
                Issues: StringOrNull(doc.RootElement, "i"),
                Review: StringOrNull(doc.RootElement, "r"));
        }
        catch (JsonException)
        {
            return new GhPullRequestCommentsEtag(Issues: null, Review: stored);
        }
    }

    public static string? Encode(string? issues, string? review)
    {
        if (issues is null && review is null)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            WriteSlot(writer, "i", issues);
            WriteSlot(writer, "r", review);
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteSlot(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    private static string? StringOrNull(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
