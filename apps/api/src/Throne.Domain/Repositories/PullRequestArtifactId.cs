namespace Throne.Domain.Repositories;

/// <summary>
/// Identifier for a <see cref="PullRequestArtifact"/>. Wire-format is the raw
/// <see cref="Value"/> string.
/// </summary>
public readonly record struct PullRequestArtifactId(string Value)
{
    public static PullRequestArtifactId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
