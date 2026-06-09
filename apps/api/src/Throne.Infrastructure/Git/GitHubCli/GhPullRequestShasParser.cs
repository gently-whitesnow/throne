using System.Text.Json;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Extracts the SHAs that anchor a GitHub review comment (<c>base.sha</c>,
/// <c>head.sha</c>) from a <c>GET /repos/{o}/{r}/pulls/{n}</c> JSON body. The
/// snapshot parser only keeps lifecycle state, so this lives separately.
/// </summary>
internal static class GhPullRequestShasParser
{
    public readonly record struct PullRequestShas(string BaseSha, string HeadSha);

    public static PullRequestShas? Parse(string json)
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
        var baseSha = GhJson.NestedString(doc.RootElement, "base", "sha");
        var headSha = GhJson.NestedString(doc.RootElement, "head", "sha");
        if (string.IsNullOrEmpty(baseSha) || string.IsNullOrEmpty(headSha))
        {
            return null;
        }
        return new PullRequestShas(baseSha, headSha);
    }
}
