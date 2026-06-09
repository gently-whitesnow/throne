using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses the JSON body of a <c>resolveReviewThread</c>/<c>unresolveReviewThread</c>
/// graphql mutation into a <see cref="ReviewThreadState"/>. <c>gh api graphql</c>
/// returns the payload on stdout (no <c>-i</c> framing) and surfaces failures as a
/// top-level <c>errors</c> array even on exit 0.
/// </summary>
internal static class GhReviewThreadMutationParser
{
    public static bool HasErrors(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    public static ReviewThreadState Parse(string json, string mutation)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("data", out var data)
            || !data.TryGetProperty(mutation, out var payload)
            || !payload.TryGetProperty("thread", out var thread)
            || thread.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"gh graphql {mutation} response missing data.{mutation}.thread.");
        }
        var id = GhJson.String(thread, "id")
            ?? throw new FormatException($"gh graphql {mutation} thread has no id.");
        return new ReviewThreadState(id, GhJson.Bool(thread, "isResolved"));
    }
}
