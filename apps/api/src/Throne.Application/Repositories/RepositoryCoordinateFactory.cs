using Throne.Application.Errors;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <summary>
/// Builds a <see cref="RepoCoordinate"/> from raw HTTP path / body values, translating the
/// domain guard's <see cref="ArgumentException"/> into the typed <see cref="ApiException"/>
/// contract (<see cref="ErrorCodes.RepositoryCoordinateInvalid"/> → 422). The coordinate
/// addressed HTTP endpoints share this so an unknown provider or a malformed owner/repo never
/// surfaces as an unhandled 500.
/// </summary>
public static class RepositoryCoordinateFactory
{
    public static RepoCoordinate Create(string provider, string owner, string repo)
    {
        try
        {
            return new RepoCoordinate(provider, owner, repo);
        }
        catch (ArgumentException ex)
        {
            throw new ApiException(
                ErrorCodes.RepositoryCoordinateInvalid,
                ex.Message,
                new Dictionary<string, object?>
                {
                    ["provider"] = provider,
                    ["owner"] = owner,
                    ["repo"] = repo,
                });
        }
    }
}
