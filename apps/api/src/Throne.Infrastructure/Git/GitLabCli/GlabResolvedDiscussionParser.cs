using System.Text.Json;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Reads the resolution state back from GitLab's
/// <c>PUT /merge_requests/:iid/discussions/:id?resolved=</c> response. The body is
/// the discussion JSON whose <c>notes[]</c> each carry a <c>resolved</c> flag —
/// the first resolvable note reflects the toggle just applied.
/// </summary>
internal static class GlabResolvedDiscussionParser
{
    public static bool ReadResolved(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object
            || !doc.RootElement.TryGetProperty("notes", out var notes)
            || notes.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var note in notes.EnumerateArray())
        {
            if (note.ValueKind == JsonValueKind.Object && GlabJson.Bool(note, "resolvable"))
            {
                return GlabJson.Bool(note, "resolved");
            }
        }
        return false;
    }
}
