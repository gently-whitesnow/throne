using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses the JSON body of <c>GET /repos/{o}/{r}/commits/{sha}</c> into a
/// provider-neutral <see cref="PullRequestDiff"/> for <c>scope=commit</c>.
/// </summary>
internal static class GhCommitDiffParser
{
    public static PullRequestDiff? Parse(string json, string requestedSha)
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
        var sha = GhJson.String(root, "sha") ?? requestedSha;
        var parentSha = ReadParentSha(root) ?? sha;
        var files = root.TryGetProperty("files", out var filesArray)
            ? GhPullRequestDiffParser.ParseFromFilesArray(filesArray)
            : Array.Empty<PullRequestDiffFile>();
        return new PullRequestDiff(
            BaseSha: parentSha,
            HeadSha: sha,
            StartSha: parentSha,
            Files: files);
    }

    private static string? ReadParentSha(JsonElement commit)
    {
        if (!commit.TryGetProperty("parents", out var parents) || parents.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var parent in parents.EnumerateArray())
        {
            var sha = GhJson.String(parent, "sha");
            if (!string.IsNullOrEmpty(sha))
            {
                return sha;
            }
        }
        return null;
    }
}
