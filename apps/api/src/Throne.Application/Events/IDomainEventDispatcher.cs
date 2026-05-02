namespace Throne.Application.Events;

/// <summary>
/// Application-layer port. Fans events out to every registered handler.
/// Implementation lives in Throne.Api so that handlers (e.g. realtime SSE publisher)
/// can map domain events into transport-specific payloads.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IDomainEvent evt, CancellationToken ct);

    Task DispatchAllAsync(IEnumerable<IDomainEvent> events, CancellationToken ct);
}

/// <summary>
/// Implement to react to a domain event. Multiple handlers can subscribe to the
/// same event; the dispatcher invokes them sequentially. A handler must filter
/// on <c>switch (evt)</c> if it cares about a specific subset.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Domain-event-handler is the canonical name for this role; rename hides intent.")]
public interface IDomainEventHandler
{
    Task HandleAsync(IDomainEvent evt, CancellationToken ct);
}
