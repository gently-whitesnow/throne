using Throne.Application.Events;
using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for <see cref="RepositoryArtifact"/> knowledge pages and their
/// append-only version history (ADR-0031). A single <see cref="WriteAsync"/> upsert covers
/// create and update: the first write (no/zero <c>expected_version</c>) yields version 1;
/// an update requires <c>expected_version == current</c>, otherwise
/// <see cref="WriteRepositoryArtifactOutcome.VersionConflict"/>.
/// </summary>
public interface IRepositoryArtifactRepository
{
    Task<WriteRepositoryArtifactOutcome> WriteAsync(
        RepoCoordinate coordinate,
        string slug,
        string title,
        string document,
        int? expectedVersion,
        DateTimeOffset now,
        CancellationToken ct);

    Task<RepositoryArtifact?> FindBySlugAsync(RepoCoordinate coordinate, string slug, CancellationToken ct);

    Task<IReadOnlyList<RepositoryArtifact>> ListByCoordinateAsync(RepoCoordinate coordinate, CancellationToken ct);

    /// <summary>Version history for one artifact, ordered by <c>version</c> ASC.</summary>
    Task<IReadOnlyList<RepositoryArtifactVersion>> ListVersionsAsync(
        RepositoryArtifactId artifactId,
        CancellationToken ct);
}

public abstract record WriteRepositoryArtifactOutcome : IDomainEventCarrier
{
    private WriteRepositoryArtifactOutcome() { }

    public virtual IReadOnlyList<IDomainEvent> Events => [];

    /// <param name="Created"><c>true</c> when this write created version 1, <c>false</c> on update.</param>
    public sealed record Written(RepositoryArtifact Artifact, bool Created) : WriteRepositoryArtifactOutcome
    {
        public override IReadOnlyList<IDomainEvent> Events => [new RepositoryDocumentUpdated(Artifact)];
    }

    public sealed record VersionConflict(int CurrentVersion) : WriteRepositoryArtifactOutcome;
}
