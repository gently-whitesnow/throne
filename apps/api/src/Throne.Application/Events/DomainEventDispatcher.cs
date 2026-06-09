using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Throne.Application.Events;

/// <summary>
/// Default dispatcher that fans events out to every <see cref="IDomainEventHandler"/>
/// registered in DI. Sequential to keep ordering predictable per UoW invocation.
///
/// Logs a per-(event, handler) trail at Debug level — the only way to confirm
/// post-mortem that a handler was actually invoked when an event-driven side-effect
/// (e.g. tmux teardown on intent → done) is later observed as missing. Exceptions
/// from handlers are logged at Warning and rethrown unchanged, preserving the
/// historical «one bad handler aborts the chain» behaviour.
/// </summary>
public sealed partial class DomainEventDispatcher(
    IEnumerable<IDomainEventHandler> handlers,
    ILogger<DomainEventDispatcher> logger) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IDomainEvent evt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var eventType = evt.GetType().Name;
        foreach (var handler in handlers)
        {
            var handlerType = handler.GetType().Name;
            var sw = Stopwatch.StartNew();
            try
            {
                await handler.HandleAsync(evt, ct);
                sw.Stop();
                LogHandlerOk(logger, eventType, handlerType, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogHandlerThrew(logger, eventType, handlerType, sw.ElapsedMilliseconds, ex);
                throw;
            }
        }
    }

    public async Task DispatchAllAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var evt in events)
        {
            await DispatchAsync(evt, ct);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "DomainEventDispatcher: event={EventType} handler={HandlerType} ok in {ElapsedMs} ms.")]
    private static partial void LogHandlerOk(
        ILogger logger, string eventType, string handlerType, long elapsedMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "DomainEventDispatcher: event={EventType} handler={HandlerType} threw after {ElapsedMs} ms — rethrowing.")]
    private static partial void LogHandlerThrew(
        ILogger logger, string eventType, string handlerType, long elapsedMs, Exception ex);
}
