using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record RegisteredRepository(RepoCoordinate Coordinate, IReadOnlyList<string> DocumentSlugs);

/// <summary>
/// Read path for registered coordinates plus the slugs of any knowledge pages already
/// written to each, so callers can match a clone's <c>git remote</c> and see which
/// pages already exist before writing.
/// </summary>
public sealed class ListRepositoriesHandler(
    IRepositoryRegistry registry,
    IRepositoryArtifactRepository artifacts)
{
    public async Task<IReadOnlyList<RegisteredRepository>> HandleAsync(CancellationToken ct)
    {
        var repositories = await registry.ListAsync(ct);
        var result = new List<RegisteredRepository>(repositories.Count);
        foreach (var repository in repositories)
        {
            var pages = await artifacts.ListByCoordinateAsync(repository.Coordinate, ct);
            result.Add(new RegisteredRepository(
                repository.Coordinate,
                pages.Select(p => p.Slug).ToList()));
        }
        return result;
    }
}
