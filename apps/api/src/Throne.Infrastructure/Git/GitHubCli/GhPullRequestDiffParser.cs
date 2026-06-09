using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses GitHub's <c>files[]</c> JSON shape — produced by either
/// <c>GET /pulls/{n}/files</c> (--paginate concatenates pages back-to-back) or
/// the <c>files</c> array embedded in <c>GET /commits/{sha}</c> — into a
/// provider-neutral <see cref="PullRequestDiffFile"/> list.
/// </summary>
internal static class GhPullRequestDiffParser
{
    public static IReadOnlyList<PullRequestDiffFile> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<PullRequestDiffFile>();
        }

        var result = new List<PullRequestDiffFile>();
        // The PR /files endpoint with --paginate concatenates [..][..]; gh
        // collapses these into a single top-level array on some versions but
        // not others. The same forward-only reader trick used for glab is
        // safe here as well.
        var reader = new System.Text.Json.Utf8JsonReader(
            System.Text.Encoding.UTF8.GetBytes(json),
            new JsonReaderOptions { AllowMultipleValues = true });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new FormatException("gh files endpoint returned non-array JSON payload.");
            }
            using var doc = JsonDocument.ParseValue(ref reader);
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var file = ProjectFile(element);
                if (file is not null)
                {
                    result.Add(file);
                }
            }
        }
        return result;
    }

    public static IReadOnlyList<PullRequestDiffFile> ParseFromFilesArray(JsonElement filesArray)
    {
        if (filesArray.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PullRequestDiffFile>();
        }
        var result = new List<PullRequestDiffFile>(filesArray.GetArrayLength());
        foreach (var element in filesArray.EnumerateArray())
        {
            var file = ProjectFile(element);
            if (file is not null)
            {
                result.Add(file);
            }
        }
        return result;
    }

    private static PullRequestDiffFile? ProjectFile(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var path = GhJson.String(element, "filename");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        return new PullRequestDiffFile(
            Path: path,
            PreviousPath: GhJson.String(element, "previous_filename"),
            Status: MapStatus(GhJson.String(element, "status")),
            Patch: GhJson.String(element, "patch") ?? string.Empty);
    }

    private static PullRequestDiffFileStatus MapStatus(string? raw) => raw switch
    {
        "added" => PullRequestDiffFileStatus.Added,
        "removed" => PullRequestDiffFileStatus.Removed,
        "renamed" => PullRequestDiffFileStatus.Renamed,
        "copied" => PullRequestDiffFileStatus.Copied,
        _ => PullRequestDiffFileStatus.Modified,
    };
}
