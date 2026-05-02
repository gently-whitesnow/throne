using Throne.Application.Ports;

namespace Throne.Application.Events;

/// <summary>
/// Decorates an inner <see cref="IUnitOfWork"/> so that whenever a unit of work
/// returns a value implementing <see cref="IDomainEventCarrier"/>, the carried
/// events are dispatched after a successful commit.
///
/// This is the pivot of the pattern: handlers stop publishing events themselves;
/// they just return outcomes. Adding a new mutation requires only that its outcome
/// carries the right events — the dispatch happens automatically.
/// </summary>
public sealed class DomainEventDispatchingUnitOfWork(
    IUnitOfWork inner,
    IDomainEventDispatcher dispatcher) : IUnitOfWork
{
    public Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        return inner.ExecuteAsync(work, ct);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        var result = await inner.ExecuteAsync(work, ct).ConfigureAwait(false);
        await DispatchIfCarrierAsync(result, ct).ConfigureAwait(false);
        return result;
    }

    public async Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        var result = await inner.ExecuteOutsideTransactionAsync(work, ct).ConfigureAwait(false);
        await DispatchIfCarrierAsync(result, ct).ConfigureAwait(false);
        return result;
    }

    private Task DispatchIfCarrierAsync<T>(T result, CancellationToken ct) =>
        result is IDomainEventCarrier carrier
            ? dispatcher.DispatchAllAsync(carrier.Events, ct)
            : Task.CompletedTask;
}
