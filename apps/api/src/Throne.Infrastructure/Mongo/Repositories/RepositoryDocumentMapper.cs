using Throne.Domain.Repositories;
using Throne.Infrastructure.Mongo.Documents;

namespace Throne.Infrastructure.Mongo.Repositories;

internal static class RepositoryDocumentMapper
{
    public static RepositoryDocument ToDocument(Repository repository) => new()
    {
        Id = repository.Id.Value,
        Provider = repository.Coordinate.Provider,
        Owner = repository.Coordinate.Owner,
        Repo = repository.Coordinate.Repo,
        CreatedAt = repository.CreatedAt.UtcDateTime,
        UpdatedAt = repository.UpdatedAt.UtcDateTime,
    };

    public static Repository ToDomain(RepositoryDocument doc) => Repository.Restore(
        id: new RepositoryId(doc.Id),
        coordinate: new RepoCoordinate(doc.Provider, doc.Owner, doc.Repo),
        createdAt: ToUtc(doc.CreatedAt),
        updatedAt: ToUtc(doc.UpdatedAt));

    private static DateTimeOffset ToUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
