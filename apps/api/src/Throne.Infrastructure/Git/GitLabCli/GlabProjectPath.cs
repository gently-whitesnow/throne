namespace Throne.Infrastructure.Git.GitLabCli;

internal static class GlabProjectPath
{
    public static string FullPath(string owner, string repo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        return $"{owner}/{repo}";
    }

    public static string ApiId(string owner, string repo) =>
        Uri.EscapeDataString(FullPath(owner, repo));
}
