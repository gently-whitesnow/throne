using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Reads the parent SHA from a single-commit response
/// (<c>GET projects/:id/repository/commits/:sha</c>) so the commit-scope diff
/// can carry its <c>base_sha</c>.
/// </summary>
internal static class GlabCommitDiffParser
{
    public static string? ReadParentSha(string commitJson)
    {
        if (string.IsNullOrWhiteSpace(commitJson))
        {
            return null;
        }
        using var doc = JsonDocument.Parse(commitJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!doc.RootElement.TryGetProperty("parent_ids", out var parents) || parents.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        foreach (var parent in parents.EnumerateArray())
        {
            if (parent.ValueKind == JsonValueKind.String)
            {
                var sha = parent.GetString();
                if (!string.IsNullOrEmpty(sha))
                {
                    return sha;
                }
            }
        }
        return null;
    }
}
