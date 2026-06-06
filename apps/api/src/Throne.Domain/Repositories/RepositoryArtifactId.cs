namespace Throne.Domain.Repositories;

/// <summary>
/// Identifier for a <see cref="RepositoryArtifact"/>. Wire-format is the raw
/// <see cref="Value"/> string (no prefix).
/// </summary>
public readonly record struct RepositoryArtifactId(string Value)
{
    public static RepositoryArtifactId New() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}
