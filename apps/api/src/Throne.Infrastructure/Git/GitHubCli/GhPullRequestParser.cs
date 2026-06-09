using System.Text.Json;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses the JSON body of <c>GET /repos/{owner}/{repo}/pulls/{number}</c> into
/// a <see cref="PullRequestSnapshot"/>. GitHub returns <c>state</c> as
/// <c>"open"</c> or <c>"closed"</c>; a closed PR with <c>merged: true</c> is
/// mapped to <see cref="PullRequestStateNames.Merged"/> so the binding's
/// <c>pull_request_state</c> column carries the lifecycle bucket the polling
/// service wants — open keeps polling, closed/merged stop.
/// </summary>
internal static class GhPullRequestParser
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
        var number = ReadNumber(root, requestedNumber);
        var state = MapState(GhJson.String(root, "state"), GhJson.Bool(root, "merged"));
        return new PullRequestSnapshot(
            Number: number,
            State: state,
            Title: GhJson.String(root, "title"),
            HtmlUrl: GhJson.String(root, "html_url"),
            Body: GhJson.String(root, "body"),
            AuthorLogin: GhJson.NestedString(root, "user", "login"),
            AuthorAvatarUrl: GhJson.NestedString(root, "user", "avatar_url"),
            HeadRef: GhJson.NestedString(root, "head", "ref"),
            BaseRef: GhJson.NestedString(root, "base", "ref"));
    }

    private static int ReadNumber(JsonElement root, int fallback)
    {
        if (root.TryGetProperty("number", out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string MapState(string? rawState, bool merged)
    {
        if (merged)
        {
            return PullRequestStateNames.Merged;
        }

        return string.Equals(rawState, "closed", StringComparison.OrdinalIgnoreCase)
            ? PullRequestStateNames.Closed
            : PullRequestStateNames.Open;
    }
}
