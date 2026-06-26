namespace Throne.Application.Ports;

/// <summary>
/// Boundary for transactional and non-transactional units of work. Both methods are
/// wrapped by a domain-event-dispatching decorator: if the returned value implements
/// <see cref="Throne.Application.Events.IDomainEventCarrier"/>, its events are
/// dispatched after the work completes successfully. Handlers therefore never publish
/// realtime / external events by hand — they simply return outcomes.
///
/// Use <see cref="ExecuteAsync{T}"/> when the work needs one atomic persistence
/// transaction. Use <see cref="ExecuteOutsideTransactionAsync{T}"/> for operations
/// that cannot or should not be wrapped in that transaction but still need their
/// domain events dispatched.
/// </summary>
public interface IUnitOfWork
{
    Task ExecuteAsync(Func<CancellationToken, Task> work, CancellationToken ct);

    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);

    Task<T> ExecuteOutsideTransactionAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct);
}
