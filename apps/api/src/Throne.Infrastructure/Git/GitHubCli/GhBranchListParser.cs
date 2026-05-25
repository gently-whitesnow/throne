using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Parses <c>gh api /repos/{owner}/{repo}</c> (for <c>default_branch</c>) and
/// <c>gh api /repos/{owner}/{repo}/branches</c> (array of <c>{ name }</c>) for
/// <see cref="GhBranchLister"/>. Defensive — missing optional fields degrade to
/// <see langword="null"/> rather than throwing.
/// </summary>
internal static class GhBranchListParser
{
    public static string? ParseDefault(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object
            ? GhJson.String(doc.RootElement, "default_branch")
            : null;
    }

    public static IReadOnlyList<GitBranchRef> ParseBranches(string json, string? defaultBranch)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GitBranchRef>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("gh api branches returned non-array JSON payload.");
        }

        var result = new List<GitBranchRef>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name = GhJson.String(item, "name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var isDefault = !string.IsNullOrEmpty(defaultBranch)
                && string.Equals(name, defaultBranch, StringComparison.Ordinal);
            result.Add(new GitBranchRef(name, isDefault));
        }

        return result;
    }
}
