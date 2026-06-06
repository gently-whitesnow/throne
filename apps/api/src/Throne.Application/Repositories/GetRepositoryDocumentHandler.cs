using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

public sealed record GetRepositoryDocumentQuery(RepoCoordinate Coordinate, string Slug);

/// <summary>
/// Thin read path over <see cref="IRepositoryArtifactRepository.FindBySlugAsync"/> for the
/// <c>get_repository_document</c> MCP tool (ADR-0031). Returns the page so the agent can read
/// its <c>version</c> back as the next write's <c>expected_version</c>; a missing page maps to
/// the typed <see cref="ApiException"/> contract.
/// </summary>
public sealed class GetRepositoryDocumentHandler(IRepositoryArtifactRepository artifacts)
{
    public async Task<RepositoryArtifact> HandleAsync(GetRepositoryDocumentQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        var artifact = await artifacts.FindBySlugAsync(query.Coordinate, query.Slug, ct);
        return artifact ?? throw NotFound(query);
    }

    private static ApiException NotFound(GetRepositoryDocumentQuery query) => new(
        ErrorCodes.RepositoryArtifactNotFound,
        $"Repository document '{query.Slug}' not found for {query.Coordinate.FullName}.",
        new Dictionary<string, object?>
        {
            ["provider"] = query.Coordinate.Provider,
            ["owner"] = query.Coordinate.Owner,
            ["repo"] = query.Coordinate.Repo,
            ["slug"] = query.Slug,
        });
}
