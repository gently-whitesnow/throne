using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record RegisteredRepository(RepoCoordinate Coordinate, IReadOnlyList<string> DocumentSlugs);

/// <summary>
/// Read path behind the <c>list_repositories</c> MCP tool (ADR-0030/0031): the registered
/// coordinates plus the slugs of any knowledge pages already written to each, so the agent
/// can match its current clone's <c>git remote</c> and see whether <c>db-schema-map</c>
/// already exists before writing.
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
