namespace Throne.Domain.Repositories;

/// <summary>
/// Titled markdown knowledge page anchored to a repository <see cref="RepoCoordinate"/>
/// (ADR-0031). A separate aggregate from <see cref="Repository"/>: the markdown body plus
/// its version history would bloat the registry root and couple their concurrency — the
/// same reasoning that keeps <c>TextVersion</c> out of <c>Intent</c> (ADR-0002).
///
/// <see cref="Slug"/> is unique per coordinate; <see cref="Version"/> drives optimistic
/// concurrency on update (enforced at the persistence boundary). Rich-DDD per ADR-0025:
/// invariants live inline in <see cref="Create"/> / <see cref="Restore"/> / <see cref="Update"/>.
/// </summary>
public sealed class RepositoryArtifact
{
    public const int MaxTitleLength = 200;

    private RepositoryArtifact(
        RepositoryArtifactId id,
        RepoCoordinate coordinate,
        string slug,
        string title,
        string document,
        int version,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        Id = id;
        Coordinate = coordinate;
        Slug = slug;
        Title = title;
        Document = document;
        Version = version;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public RepositoryArtifactId Id { get; }
    public RepoCoordinate Coordinate { get; }
    public string Slug { get; }
    public string Title { get; private set; }
    public string Document { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static RepositoryArtifact Create(
        RepositoryArtifactId id,
        RepoCoordinate coordinate,
        string slug,
        string title,
        string document,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        RepositoryArtifactSlugGuard.EnsureValid(slug);
        EnsureValidTitle(title);
        ArgumentNullException.ThrowIfNull(document);

        return new RepositoryArtifact(id, coordinate, slug, title, document, version: 1, now, now);
    }

    public static RepositoryArtifact Restore(RepositoryArtifactSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(snapshot.Coordinate);
        RepositoryArtifactSlugGuard.EnsureValid(snapshot.Slug);
        EnsureValidTitle(snapshot.Title);
        ArgumentNullException.ThrowIfNull(snapshot.Document);
        if (snapshot.Version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "version must be >= 1.");
        }

        return new RepositoryArtifact(
            snapshot.Id, snapshot.Coordinate, snapshot.Slug, snapshot.Title, snapshot.Document,
            snapshot.Version, snapshot.CreatedAt, snapshot.UpdatedAt);
    }

    /// <summary>
    /// Apply a new revision: replace the editable fields and bump <see cref="Version"/>.
    /// The <c>expected_version == current</c> precondition is checked at the persistence
    /// boundary (mirrors Intent/Instruction/Tag); this method only advances the aggregate.
    /// </summary>
    public void Update(string title, string document, DateTimeOffset now)
    {
        EnsureValidTitle(title);
        ArgumentNullException.ThrowIfNull(document);

        Title = title;
        Document = document;
        Version += 1;
        UpdatedAt = now;
    }

    private static void EnsureValidTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        if (title.Length > MaxTitleLength)
        {
            throw new ArgumentException($"title exceeds {MaxTitleLength} characters.", nameof(title));
        }
    }
}
