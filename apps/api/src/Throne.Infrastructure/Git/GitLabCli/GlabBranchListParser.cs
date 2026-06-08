using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabBranchListParser
{
    public static IReadOnlyList<GitBranchRef> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<GitBranchRef>();
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("glab api branches returned non-array JSON payload.");
        }

        var result = new List<GitBranchRef>(doc.RootElement.GetArrayLength());
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name = GlabJson.String(item, "name");
            if (!string.IsNullOrEmpty(name))
            {
                result.Add(new GitBranchRef(name, GlabJson.Bool(item, "default")));
            }
        }

        return result;
    }
}
