namespace Throne.Infrastructure.EfCore.Rows;

/// <summary>
/// Persistence POCO for the singleton <c>gitlab_host_settings</c> table. Holds the
/// operator-configured GitLab hostname used by <c>glab</c>. Default <c>gitlab.com</c> is
/// materialised on first read.
/// </summary>
internal sealed class GitLabHostSettingsRow
{
    public const string SingletonId = "singleton";

    public string Id { get; set; } = SingletonId;
    public string Host { get; set; } = string.Empty;
}
