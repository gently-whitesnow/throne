using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class RepositoryArtifactDocumentMapper
{
    public static RepositoryArtifactDocument ToDocument(RepositoryArtifact artifact) => new()
    {
        Id = artifact.Id.Value,
        Provider = artifact.Coordinate.Provider,
        Owner = artifact.Coordinate.Owner,
        Repo = artifact.Coordinate.Repo,
        Slug = artifact.Slug,
        Title = artifact.Title,
        Document = artifact.Document,
        RenderHint = artifact.RenderHint,
        Version = artifact.Version,
        CreatedAt = artifact.CreatedAt.UtcDateTime,
        UpdatedAt = artifact.UpdatedAt.UtcDateTime,
    };

    public static RepositoryArtifact ToDomain(RepositoryArtifactDocument doc) =>
        RepositoryArtifact.Restore(new RepositoryArtifactSnapshot(
            Id: new RepositoryArtifactId(doc.Id),
            Coordinate: new RepoCoordinate(doc.Provider, doc.Owner, doc.Repo),
            Slug: doc.Slug,
            Title: doc.Title,
            Document: doc.Document,
            RenderHint: doc.RenderHint,
            Version: doc.Version,
            CreatedAt: ToUtc(doc.CreatedAt),
            UpdatedAt: ToUtc(doc.UpdatedAt)));

    public static RepositoryArtifactVersionDocument ToVersionDocument(RepositoryArtifactVersion version) => new()
    {
        Id = version.Id,
        ArtifactId = version.ArtifactId.Value,
        Version = version.Version,
        Title = version.Title,
        Document = version.Document,
        RenderHint = version.RenderHint,
        CreatedAt = version.CreatedAt.UtcDateTime,
    };

    public static RepositoryArtifactVersion ToVersionDomain(RepositoryArtifactVersionDocument doc) => new(
        Id: doc.Id,
        ArtifactId: new RepositoryArtifactId(doc.ArtifactId),
        Version: doc.Version,
        Title: doc.Title,
        Document: doc.Document,
        RenderHint: doc.RenderHint,
        CreatedAt: ToUtc(doc.CreatedAt));

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
