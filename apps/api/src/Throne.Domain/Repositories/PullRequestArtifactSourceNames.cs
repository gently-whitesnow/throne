namespace Throne.Domain.Repositories;

/// <summary>Wire-format constants for <see cref="PullRequestArtifact.Source"/>.</summary>
public static class PullRequestArtifactSourceNames
{
    public const string Static = "static";
    public const string Agent = "agent";

    public static bool IsKnown(string value) => value is Static or Agent;
}
