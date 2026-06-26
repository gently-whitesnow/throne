using Throne.Domain.Repositories;
using Throne.Infrastructure.EfCore.Rows;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class RepositoryRowMapper
{
    public static RepositoryRow ToRow(Repository repository) => new()
    {
        Id = repository.Id.Value,
        Provider = repository.Coordinate.Provider,
        // RepoCoordinate already normalizes Host: GitHub → "github.com", GitLab → bare hostname.
        // Storing the normalized value collapses the unique key correctly across both backends.
        Host = repository.Coordinate.Host,
        Owner = repository.Coordinate.Owner,
        Repo = repository.Coordinate.Repo,
        ProjectId = repository.Coordinate.ProjectId,
        CreatedAt = repository.CreatedAt,
        UpdatedAt = repository.UpdatedAt,
    };

    public static Repository ToDomain(RepositoryRow row) => Repository.Restore(
        id: new RepositoryId(row.Id),
        coordinate: new RepoCoordinate(row.Provider, row.Owner, row.Repo, row.Host, row.ProjectId),
        createdAt: row.CreatedAt,
        updatedAt: row.UpdatedAt);
}
