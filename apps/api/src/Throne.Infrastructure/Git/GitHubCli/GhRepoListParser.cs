using System.Text.Json;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses <c>gh repo list --json name,owner,defaultBranchRef,description,isPrivate,url,nameWithOwner</c>.
/// Field names follow <c>gh</c>'s camelCase JSON projection (verified against gh 2.x).
/// Defensive: each object is a best-effort projection — missing optional fields
/// degrade to <see langword="null"/> rather than throwing.
/// </summary>
internal static class GhRepoListParser
{
    public static IReadOnlyList<GitRepositoryRef> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GitRepositoryRef>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("gh repo list returned non-array JSON payload.");
        }

        var result = new List<GitRepositoryRef>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var repo = TryProject(item);
            if (repo is not null)
            {
                result.Add(repo);
            }
        }

        return result;
    }

    private static GitRepositoryRef? TryProject(JsonElement item)
    {
        var name = GhJson.String(item, "name");
        var owner = GhJson.NestedString(item, "owner", "login");
        var defaultBranch = GhJson.NestedString(item, "defaultBranchRef", "name");
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(defaultBranch))
        {
            return null;
        }

        return new GitRepositoryRef(
            Provider: GitProviderNames.GitHub,
            Owner: owner,
            Repo: name,
            DefaultBranch: defaultBranch)
        {
            Description = GhJson.String(item, "description"),
            Private = GhJson.Bool(item, "isPrivate"),
            HtmlUrl = GhJson.String(item, "url"),
        };
    }
}
