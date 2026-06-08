using System.Text.Json;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabRepoListParser
{
    public static IReadOnlyList<GitRepositoryRef> Parse(string json, string host)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GitRepositoryRef>();
        }

        using var doc = JsonDocument.Parse(NormalisePaginated(json));
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("glab api projects returned non-array JSON payload.");
        }

        var result = new List<GitRepositoryRef>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var repo = TryProject(item, host);
            if (repo is not null)
            {
                result.Add(repo);
            }
        }

        return result;
    }

    private static GitRepositoryRef? TryProject(JsonElement item, string host)
    {
        var fullPath = GlabJson.String(item, "path_with_namespace");
        var defaultBranch = GlabJson.String(item, "default_branch");
        if (string.IsNullOrEmpty(fullPath) || string.IsNullOrEmpty(defaultBranch))
        {
            return null;
        }

        var split = SplitFullPath(fullPath);
        if (split is null)
        {
            return null;
        }

        return new GitRepositoryRef(
            Provider: GitProviderNames.GitLab,
            Owner: split.Value.Owner,
            Repo: split.Value.Repo,
            DefaultBranch: defaultBranch,
            Host: host,
            ProjectId: GlabJson.Int(item, "id"))
        {
            Description = GlabJson.String(item, "description"),
            Private = string.Equals(GlabJson.String(item, "visibility"), "private", StringComparison.OrdinalIgnoreCase),
            HtmlUrl = GlabJson.String(item, "web_url"),
        };
    }

    private static (string Owner, string Repo)? SplitFullPath(string fullPath)
    {
        var slash = fullPath.LastIndexOf('/');
        if (slash <= 0 || slash == fullPath.Length - 1)
        {
            return null;
        }

        return (fullPath[..slash], fullPath[(slash + 1)..]);
    }

    private static string NormalisePaginated(string raw) =>
        raw.Replace("][", ",", StringComparison.Ordinal);
}
