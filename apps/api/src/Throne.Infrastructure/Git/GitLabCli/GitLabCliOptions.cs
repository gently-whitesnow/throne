namespace Throne.Infrastructure.Git.GitLabCli;

public sealed class GitLabCliOptions
{
    public const string SectionName = "Throne:GitLabCli";

    public string ExecutablePath { get; set; } = "glab";

    public string GitExecutablePath { get; set; } = "git";

    public int PageSize { get; set; } = 50;

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan CloneTimeout { get; set; } = TimeSpan.FromMinutes(10);
}
