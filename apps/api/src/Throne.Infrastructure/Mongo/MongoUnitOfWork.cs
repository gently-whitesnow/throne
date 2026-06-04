using MongoDB.Driver;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Mongo;

internal sealed class MongoUnitOfWork(IMongoClient client, MongoSessionAccessor accessor) : IUnitOfWork
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

        using var session = await client.StartSessionAsync(cancellationToken: ct);
        using var scope = accessor.BeginScope(session);

        // WithTransactionAsync — клиентский retry, который MongoDB штатно ожидает:
        // на TransientTransactionError повторяется весь callback (например, WriteConflict
        // или каталоговая гонка SnapshotUnavailable при неявном создании коллекций),
        // на UnknownTransactionCommitResult — сам commit. Иначе эти транзиентные ошибки
        // пробрасываются наружу и дают недетерминированную флакость.
        return await session.WithTransactionAsync(
            async (_, innerCt) => await work(innerCt),
            cancellationToken: ct);
    }

    public Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        return work(ct);
    }
}
