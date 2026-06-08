namespace Throne.Application.Git;

public sealed class GitLabSettings
{
    public const string SectionName = "Throne:GitLab";

    public string? Host { get; set; }
}
