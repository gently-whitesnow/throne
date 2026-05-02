namespace Throne.Application.Events;

/// <summary>
/// Marker for application/domain events that signal a state change worth fanning out.
/// Events are produced by repositories on the success branches of their outcomes,
/// drained from <see cref="IDomainEventCarrier"/> by the unit-of-work decorator,
/// and dispatched to every registered <see cref="IDomainEventHandler"/>.
///
/// Today the only handler is the realtime SSE publisher; tomorrow the same pipeline
/// will feed an external broker, denormalized read models, or audit/history sinks.
/// </summary>
public interface IDomainEvent;
