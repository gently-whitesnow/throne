using Throne.Domain.Repositories;

namespace Throne.Application.Ports;

/// <summary>
/// Persistence boundary for the <see cref="Repository"/> registry (ADR-0031). The single
/// source that materialises a registry row for a coordinate the first time it surfaces —
/// from a bind, a tag default, or a knowledge-page write. Idempotent: a coordinate already
/// present is returned unchanged.
///
/// This is NOT an FK rewrite — <c>IntentRepositoryBinding</c> / <c>TagDefaultRepository</c>
/// keep relating by coordinate, not by <see cref="RepositoryId"/>.
/// </summary>
public interface IRepositoryRegistry
{
    /// <summary>
    /// Insert the registry row for <paramref name="coordinate"/> if absent, otherwise return
    /// the existing row. Must run inside <see cref="IUnitOfWork.ExecuteAsync"/> when the caller
    /// wants it in the surrounding transaction; the implementation honours the ambient session.
    /// </summary>
    Task<Repository> EnsureRepositoryAsync(RepoCoordinate coordinate, DateTimeOffset now, CancellationToken ct);

    Task<Repository?> FindByCoordinateAsync(RepoCoordinate coordinate, CancellationToken ct);

    /// <summary>
    /// Every registered repository, ordered by coordinate. Backs the <c>list_repositories</c>
    /// read tool (ADR-0030): a session without an intent has no coordinate of its own, so the
    /// agent discovers known repositories from the registry plus the current clone's
    /// <c>git remote</c>.
    /// </summary>
    Task<IReadOnlyList<Repository>> ListAsync(CancellationToken ct);
}
