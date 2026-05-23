using System.Text.Json;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses <c>gh api /user/repos</c> responses (snake_case GitHub REST projection,
/// distinct from <c>gh repo list --json</c>'s camelCase). Used for the
/// <c>involved</c> search scope per ADR-0024 § 3 — collaborator and org-member
/// repos that <c>gh repo list</c> does not surface by default.
/// </summary>
internal static class GhUserReposParser
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
            throw new FormatException("gh api /user/repos returned non-array JSON payload.");
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
        var defaultBranch = GhJson.String(item, "default_branch");
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
            Private = GhJson.Bool(item, "private"),
            HtmlUrl = GhJson.String(item, "html_url"),
        };
    }
}
