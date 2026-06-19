using Throne.Domain.Repositories;
using Throne.Repositories.Contracts.Generated;

namespace Throne.Api.Repositories;

/// <summary>
/// Maps the <see cref="Repository"/> registry aggregate and the <see cref="RepositoryArtifact"/>
/// knowledge-page aggregate (+ its version history) to their wire DTOs. The markdown body is
/// dropped in the list projection (<see cref="ToDocumentSummaryDto"/>).
/// </summary>
internal static class RepositoryRegistryDtoMapper
{
    public static RepositoryDto ToRepositoryDto(Repository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return new RepositoryDto
        {
            Provider = RepositoryEnumDtoMapper.ToWireProvider(repository.Coordinate.Provider),
            Host = repository.Coordinate.Host,
            Owner = repository.Coordinate.Owner,
            Repo = repository.Coordinate.Repo,
            Project_id = repository.Coordinate.ProjectId,
            Full_name = repository.Coordinate.FullName,
            Created_at = repository.CreatedAt,
            Updated_at = repository.UpdatedAt,
        };
    }

    public static RepositoryDocumentSummaryDto ToDocumentSummaryDto(RepositoryArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new RepositoryDocumentSummaryDto
        {
            Slug = artifact.Slug,
            Title = artifact.Title,
            Version = artifact.Version,
            Created_at = artifact.CreatedAt,
            Updated_at = artifact.UpdatedAt,
        };
    }

    public static RepositoryDocumentDto ToDocumentDto(RepositoryArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return new RepositoryDocumentDto
        {
            Provider = RepositoryEnumDtoMapper.ToWireProvider(artifact.Coordinate.Provider),
            Host = artifact.Coordinate.Host,
            Owner = artifact.Coordinate.Owner,
            Repo = artifact.Coordinate.Repo,
            Project_id = artifact.Coordinate.ProjectId,
            Slug = artifact.Slug,
            Title = artifact.Title,
            Document = artifact.Document,
            Version = artifact.Version,
            Created_at = artifact.CreatedAt,
            Updated_at = artifact.UpdatedAt,
        };
    }

    public static RepositoryDocumentVersionDto ToDocumentVersionDto(RepositoryArtifactVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new RepositoryDocumentVersionDto
        {
            Version = version.Version,
            Title = version.Title,
            Document = version.Document,
            Created_at = version.CreatedAt,
        };
    }
}
