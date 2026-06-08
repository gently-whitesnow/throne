namespace Throne.Domain.Repositories;

/// <summary>
/// Wire-format constants for supported git providers (see ADR-0024).
/// </summary>
public static class GitProviderNames
{
    public const string GitHub = "github";
    public const string GitLab = "gitlab";

    public static bool IsKnown(string value) => value is GitHub or GitLab;
}
