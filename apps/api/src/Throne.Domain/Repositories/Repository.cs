namespace Throne.Domain.Repositories;

/// <summary>
/// Thin registry row for a git repository, keyed on the <c>(provider, owner, repo)</c>
/// <see cref="RepoCoordinate"/> (ADR-0031). Metadata-only: it does NOT own the working
/// copy (that is <c>IntentRepositoryBinding</c>) and carries no user-editable content, so
/// there is deliberately no <c>version</c> / optimistic concurrency here — the row is the
/// same regardless of how the coordinate first surfaced (bind, tag default, artifact write).
///
/// Rich-DDD per ADR-0025: invariants live inline via <see cref="Create"/> / <see cref="Restore"/>.
/// </summary>
public sealed class Repository
{
    private Repository(
        RepositoryId id,
        RepoCoordinate coordinate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Coordinate = coordinate;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public RepositoryId Id { get; }
    public RepoCoordinate Coordinate { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; }

    public static Repository Create(RepositoryId id, RepoCoordinate coordinate, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new Repository(id, coordinate, now, now);
    }

    public static Repository Restore(
        RepositoryId id,
        RepoCoordinate coordinate,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new Repository(id, coordinate, createdAt, updatedAt);
    }
}
