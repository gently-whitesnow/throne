using Throne.Application.Ports;
using Throne.Domain.Repositories;

namespace Throne.Application.Repositories;

/// <param name="Created"><c>true</c> when this call materialised the registry row, <c>false</c>
/// when the coordinate was already registered (drives 201 vs 200 on the HTTP surface).</param>
public sealed record CreateRepositoryResult(Repository Repository, bool Created);

/// <summary>
/// Manual repository registration behind <c>POST /api/v1/repositories</c> (ADR-0031). Idempotent:
/// the registry upsert returns the existing row unchanged, so a probe before the write tells the
/// HTTP surface whether to answer 201 (created) or 200 (already present).
/// </summary>
public sealed class CreateRepositoryHandler(IRepositoryRegistry registry, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<CreateRepositoryResult> HandleAsync(
        string provider, string owner, string repo, CancellationToken ct)
    {
        var coordinate = RepositoryCoordinateFactory.Create(provider, owner, repo);
        var existed = await registry.FindByCoordinateAsync(coordinate, ct) is not null;
        var now = clock.GetUtcNow();
        var saved = await unitOfWork.ExecuteAsync(
            inner => registry.EnsureRepositoryAsync(coordinate, now, inner), ct);
        return new CreateRepositoryResult(saved, Created: !existed);
    }
}
