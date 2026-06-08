using Throne.Domain.Repositories;

namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabPullRequestState
{
    public static string Normalize(string? rawState) =>
        rawState?.ToLowerInvariant() switch
        {
            "closed" => PullRequestStateNames.Closed,
            "merged" => PullRequestStateNames.Merged,
            _ => PullRequestStateNames.Open,
        };
}
