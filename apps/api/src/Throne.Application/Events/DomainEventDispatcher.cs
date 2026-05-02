namespace Throne.Application.Events;

/// <summary>
/// Default dispatcher that fans events out to every <see cref="IDomainEventHandler"/>
/// registered in DI. Sequential to keep ordering predictable per UoW invocation.
/// </summary>
public sealed class DomainEventDispatcher(IEnumerable<IDomainEventHandler> handlers) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        foreach (var handler in handlers)
        {
            await handler.HandleAsync(evt, ct).ConfigureAwait(false);
        }
    }

    public async Task DispatchAllAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var evt in events)
        {
            await DispatchAsync(evt, ct).ConfigureAwait(false);
        }
    }
}
