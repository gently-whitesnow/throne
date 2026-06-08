namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabEnvironment
{
    public static IReadOnlyDictionary<string, string?> ForHost(string host) =>
        new Dictionary<string, string?> { ["GITLAB_HOST"] = host };
}
