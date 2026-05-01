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
            await work(inner).ConfigureAwait(false);
            return null;
        }, ct);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (accessor.Current is not null)
        {
            return await work(ct).ConfigureAwait(false);
        }

        using var session = await client.StartSessionAsync(cancellationToken: ct).ConfigureAwait(false);
        session.StartTransaction();
        using var scope = accessor.BeginScope(session);

        T result;
        try
        {
            result = await work(ct).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                await session.AbortTransactionAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // best-effort abort; original exception is more relevant
            }
            throw;
        }

        await session.CommitTransactionAsync(ct).ConfigureAwait(false);
        return result;
    }
}
