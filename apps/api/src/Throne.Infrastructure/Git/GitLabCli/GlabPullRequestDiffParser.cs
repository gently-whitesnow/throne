using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Parses GitLab's MR / commit diff JSON arrays (file-per-element with a
/// <c>diff</c> patch field) into provider-neutral <see cref="PullRequestDiffFile"/>.
/// Used by both the MR diff endpoint (<c>/merge_requests/:iid/diffs</c>) and the
/// commit diff endpoint (<c>/repository/commits/:sha/diff</c>).
/// </summary>
internal static class GlabPullRequestDiffParser
{
    public static IReadOnlyList<PullRequestDiffFile> Parse(string json)
    {
        var result = new List<PullRequestDiffFile>();
        GlabPaginatedJson.ForEachElement(json, element =>
        {
            var file = ProjectFile(element);
            if (file is not null)
            {
                result.Add(file);
            }
        });
        return result;
    }

    private static PullRequestDiffFile? ProjectFile(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var newPath = GlabJson.String(element, "new_path");
        var oldPath = GlabJson.String(element, "old_path");
        var path = newPath ?? oldPath;
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        return new PullRequestDiffFile(
            Path: path,
            PreviousPath: oldPath != newPath ? oldPath : null,
            Status: MapStatus(element),
            Patch: GlabJson.String(element, "diff") ?? string.Empty);
    }

    private static PullRequestDiffFileStatus MapStatus(JsonElement element)
    {
        if (GlabJson.Bool(element, "new_file"))
        {
            return PullRequestDiffFileStatus.Added;
        }
        if (GlabJson.Bool(element, "deleted_file"))
        {
            return PullRequestDiffFileStatus.Removed;
        }
        if (GlabJson.Bool(element, "renamed_file"))
        {
            return PullRequestDiffFileStatus.Renamed;
        }
        return PullRequestDiffFileStatus.Modified;
    }
}
