using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses <c>GET /repos/{o}/{r}/pulls/{n}/commits</c> (one or more JSON arrays
/// concatenated by <c>--paginate</c>) into a flat list of provider-neutral
/// <see cref="PullRequestCommitRef"/>.
/// </summary>
internal static class GhPullRequestCommitsParser
{
    public static IReadOnlyList<PullRequestCommitRef> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PullRequestCommitRef>();
        }
        var result = new List<PullRequestCommitRef>();
        var reader = new System.Text.Json.Utf8JsonReader(
            System.Text.Encoding.UTF8.GetBytes(json),
            new JsonReaderOptions { AllowMultipleValues = true });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new FormatException("gh commits endpoint returned non-array JSON payload.");
            }
            using var doc = JsonDocument.ParseValue(ref reader);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var commit = ProjectCommit(element);
                if (commit is not null)
                {
                    result.Add(commit);
                }
            }
        }
        return result;
    }

    private static PullRequestCommitRef? ProjectCommit(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var sha = GhJson.String(element, "sha");
        if (string.IsNullOrEmpty(sha))
        {
            return null;
        }
        var commit = element.TryGetProperty("commit", out var commitNode) && commitNode.ValueKind == JsonValueKind.Object
            ? commitNode
            : default;
        var message = commit.ValueKind == JsonValueKind.Object ? GhJson.String(commit, "message") ?? string.Empty : string.Empty;
        var authoredAt = ReadCommitDate(commit);
        var authorLogin = GhJson.NestedString(element, "author", "login")
            ?? (commit.ValueKind == JsonValueKind.Object ? GhJson.NestedString(commit, "author", "name") : null);
        return new PullRequestCommitRef(
            Sha: sha,
            Message: message,
            AuthorLogin: authorLogin,
            CommittedAt: authoredAt);
    }

    private static DateTimeOffset ReadCommitDate(JsonElement commit)
    {
        if (commit.ValueKind != JsonValueKind.Object)
        {
            return DateTimeOffset.MinValue;
        }
        if (commit.TryGetProperty("committer", out var committer) && committer.ValueKind == JsonValueKind.Object)
        {
            var raw = GhJson.String(committer, "date");
            if (raw is not null && DateTimeOffset.TryParse(raw, out var parsed))
            {
                return parsed;
            }
        }
        if (commit.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object)
        {
            var raw = GhJson.String(author, "date");
            if (raw is not null && DateTimeOffset.TryParse(raw, out var parsed))
            {
                return parsed;
            }
        }
        return DateTimeOffset.MinValue;
    }
}
