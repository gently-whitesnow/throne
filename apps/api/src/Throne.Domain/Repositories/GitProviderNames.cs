namespace Throne.Domain.Repositories;

/// <summary>
/// Wire-format constants for supported git providers (see ADR-0024).
/// Only <see cref="GitHub"/> is shipped today.
/// </summary>
public static class GitProviderNames
{
    public const string GitHub = "github";

    public static bool IsKnown(string value) => value is GitHub;
}
