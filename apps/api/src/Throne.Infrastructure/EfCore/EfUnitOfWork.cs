using Microsoft.EntityFrameworkCore;
using Throne.Application.Ports;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// EF Core unit-of-work implementation:
/// <list type="bullet">
/// <item><see cref="ExecuteAsync{T}"/> opens one <see cref="ThroneDbContext"/> and one
/// transaction. Repositories call <c>SaveChangesAsync</c> themselves inside the work
/// delegate so synchronous Conflict/NotFound outcomes surface before commit; the UoW
/// commits once the work returns.</item>
/// <item>A reentrant call (ambient context already present) joins it and runs the work
/// inline — no nested transaction.</item>
/// <item><see cref="ExecuteOutsideTransactionAsync{T}"/> still publishes an ambient
/// context so writes resolve, but without a transaction (BLOB attachment paths).</item>
/// </list>
/// SQLite is single-writer, so storage-level write conflicts are serialized before
/// commit; application-level CAS still lives in repository predicates.
/// </summary>
internal sealed class EfUnitOfWork(IDbContextFactory<ThroneDbContext> contextFactory, EfSessionAccessor accessor)
    : IUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        return ExecuteAsync<object?>(async inner =>
        {
            await work(inner);
            return null;
        }, ct);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (accessor.Current is not null)
        {
            return await work(ct);
        }

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        using var scope = accessor.BeginScope(context);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);

        var result = await work(ct);

        await transaction.CommitAsync(ct);
        return result;
    }

    public async Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (accessor.Current is not null)
        {
            return await work(ct);
        }

        await using var context = await contextFactory.CreateDbContextAsync(ct);
        using var scope = accessor.BeginScope(context);
        return await work(ct);
    }
}
