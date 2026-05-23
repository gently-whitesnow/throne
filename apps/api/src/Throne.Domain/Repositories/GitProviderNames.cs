namespace Throne.Domain.Repositories;

/// <summary>
/// Wire-format constants for supported git providers (see ADR-0024).
/// Slice 1 ships only <see cref="GitHub"/>; <c>gitlab</c> arrives in slice 5.
/// </summary>
public static class GitProviderNames
{
    public const string GitHub = "github";

    public static bool IsKnown(string value) => value is GitHub;
}
