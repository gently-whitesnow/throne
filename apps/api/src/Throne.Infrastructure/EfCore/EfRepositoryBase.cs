using Microsoft.EntityFrameworkCore;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// Shared scaffolding for EF Core repositories, mirroring <c>MongoRepositoryBase</c>'s
/// session-awareness split:
/// <list type="bullet">
/// <item>Reads run on the ambient unit-of-work context when one is active, otherwise on
/// a short-lived transient context created per call (and disposed here) — so queries
/// outside <c>IUnitOfWork.ExecuteAsync</c> work without a transaction.</item>
/// <item>Writes go through <see cref="RequireContext"/>, which demands the ambient
/// context: a mutation outside a unit of work is a programming error, not a sessionless
/// fallback.</item>
/// </list>
/// Repository ports (and the per-entity <c>SaveChangesAsync</c> calls that surface
/// Conflict/NotFound) arrive in slice 2; this base only owns context selection.
/// </summary>
internal abstract class EfRepositoryBase(IDbContextFactory<ThroneDbContext> contextFactory, EfSessionAccessor sessions)
{
    protected EfSessionAccessor Sessions { get; } = sessions;

    /// <summary>
    /// Runs a read against the ambient context, or a transient one disposed afterwards.
    /// </summary>
    protected async Task<TResult> ReadAsync<TResult>(
        Func<ThroneDbContext, CancellationToken, Task<TResult>> read,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(read);

        var ambient = Sessions.Current;
        if (ambient is not null)
        {
            return await read(ambient, ct);
        }

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        return await read(context, ct);
    }

    /// <summary>
    /// The ambient write context. Throws when called outside <c>IUnitOfWork.ExecuteAsync</c>
    /// / <c>ExecuteOutsideTransactionAsync</c>.
    /// </summary>
    protected ThroneDbContext RequireContext() =>
        Sessions.Current
        ?? throw new InvalidOperationException(
            "No ambient ThroneDbContext. Writes must run inside IUnitOfWork.ExecuteAsync.");
}
