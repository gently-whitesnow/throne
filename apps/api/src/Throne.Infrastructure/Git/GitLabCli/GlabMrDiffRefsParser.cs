using System.Text.Json;

namespace Throne.Infrastructure.Git.GitLabCli;

/// <summary>
/// Extracts the SHAs required to anchor a GitLab MR discussion position
/// (<c>base_sha</c>, <c>head_sha</c>, <c>start_sha</c>) from a
/// <c>GET projects/:id/merge_requests/:iid</c> response.
/// </summary>
internal static class GlabMrDiffRefsParser
{
    public readonly record struct DiffRefs(string BaseSha, string HeadSha, string StartSha);

    public static DiffRefs? Parse(string json)
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
        if (!doc.RootElement.TryGetProperty("diff_refs", out var refs) || refs.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var baseSha = GlabJson.String(refs, "base_sha");
        var headSha = GlabJson.String(refs, "head_sha");
        var startSha = GlabJson.String(refs, "start_sha");
        if (string.IsNullOrEmpty(baseSha) || string.IsNullOrEmpty(headSha) || string.IsNullOrEmpty(startSha))
        {
            return null;
        }
        return new DiffRefs(baseSha, headSha, startSha);
    }
}
