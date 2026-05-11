using Throne.Realtime.Contracts;

namespace Throne.Api.Realtime;

/// <summary>
/// In-process broadcaster for realtime events. Singleton; thread-safe.
/// Subscribers are per SSE connection; the only publisher is
/// <see cref="RealtimeDomainEventHandler"/>, which receives domain events from the
/// dispatching <see cref="Throne.Application.Ports.IUnitOfWork"/> decorator.
/// </summary>
public interface IRealtimeEventBroker
{
    /// <summary>
    /// Subscribe a new SSE connection. The returned subscription owns a bounded queue;
    /// dispose to release resources when the connection ends.
    /// </summary>
    IRealtimeEventSubscription Subscribe();

    /// <summary>Best-effort fanout to every active subscription.</summary>
    ValueTask PublishAsync(RealtimeEventEnvelope envelope, CancellationToken ct);
}

public interface IRealtimeEventSubscription : IDisposable
{
    IAsyncEnumerable<RealtimeEventEnvelope> ReadAllAsync(CancellationToken ct);
}
