namespace Throne.Domain.Repositories;

/// <summary>
/// Flat rehydration payload for <see cref="RepositoryArtifact.Restore"/>. Mirrors the
/// persisted document so a tampered row fails fast on the same invariants as a fresh write.
/// </summary>
public sealed record RepositoryArtifactSnapshot(
    RepositoryArtifactId Id,
    RepoCoordinate Coordinate,
    string Slug,
    string Title,
    string Document,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
