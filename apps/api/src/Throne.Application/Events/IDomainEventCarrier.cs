namespace Throne.Application.Events;

/// <summary>
/// Implemented by repository outcomes (and any other unit-of-work return value)
/// that may carry domain events to be dispatched after a successful commit.
///
/// The dispatching <see cref="Throne.Application.Ports.IUnitOfWork"/> decorator inspects
/// every <c>ExecuteAsync&lt;T&gt;</c> result; if <c>T</c> is <see cref="IDomainEventCarrier"/>,
/// the events are drained and routed to all registered handlers.
///
/// Failure branches (NotFound, Conflict, …) implement this interface with an empty list,
/// so the decorator stays branch-agnostic.
/// </summary>
public interface IDomainEventCarrier
{
    IReadOnlyList<IDomainEvent> Events { get; }
}
