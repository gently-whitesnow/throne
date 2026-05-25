using System.Text.Json;
using Throne.Application.Git;
using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses <c>gh pr list --json number,title,headRefName,state</c>. <c>gh</c>
/// reports <c>state</c> as <c>OPEN</c> uppercase — <see cref="GhPullRequestListProjector"/>
/// lower-cases to match the wire-format constants in <see cref="PullRequestStateNames"/>.
/// </summary>
internal static class GhPullRequestListParser
{
    public static IReadOnlyList<GitPullRequestRef> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GitPullRequestRef>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("gh pr list returned non-array JSON payload.");
        }

        var result = new List<GitPullRequestRef>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var pr = GhPullRequestListProjector.TryProject(item);
            if (pr is not null)
            {
                result.Add(pr);
            }
        }

        return result;
    }
}
