using Throne.Application.Errors;
using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Read path behind <c>GET /api/v1/repositories/{provider}/{owner}/{repo}</c>: the registry row
/// for a coordinate, or the typed <see cref="ApiException"/> (<see cref="ErrorCodes.RepositoryNotFound"/>)
/// when the coordinate has never surfaced.
/// </summary>
public sealed class GetRepositoryHandler(IRepositoryRegistry registry)
{
    public async Task<Repository> HandleAsync(string provider, string owner, string repo, CancellationToken ct)
    {
        var coordinate = RepositoryCoordinateFactory.Create(provider, owner, repo);
        return await registry.FindByCoordinateAsync(coordinate, ct) ?? throw new ApiException(
            ErrorCodes.RepositoryNotFound,
            $"Repository {coordinate.FullName} is not registered.",
            new Dictionary<string, object?>
            {
                ["provider"] = coordinate.Provider,
                ["owner"] = coordinate.Owner,
                ["repo"] = coordinate.Repo,
            });
    }
}
