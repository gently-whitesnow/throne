using System.Text.Json;
using Throne.Application.Git;

namespace Throne.Infrastructure.Git.GitHubCli;

/// <summary>
/// Per-element projector for <see cref="GhPullRequestListParser"/>.
/// </summary>
internal static class GhPullRequestListProjector
{
    public static GitPullRequestRef? TryProject(JsonElement item)
    {
        var head = GhJson.String(item, "headRefName");
        var state = GhJson.String(item, "state");
        var hasNumber = item.TryGetProperty("number", out var numEl)
            && numEl.ValueKind == JsonValueKind.Number;
        if (!hasNumber || string.IsNullOrEmpty(head) || string.IsNullOrEmpty(state))
        {
            return null;
        }

        var title = GhJson.String(item, "title") ?? string.Empty;
        return new GitPullRequestRef(numEl.GetInt32(), title, head, state.ToLowerInvariant());
    }
}
