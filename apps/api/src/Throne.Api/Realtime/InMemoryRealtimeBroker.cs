using System.Collections.Concurrent;
using System.Threading.Channels;
using Throne.Realtime.Contracts;

namespace Throne.Api.Realtime;

/// <summary>
/// In-memory fan-out: each subscription owns a bounded channel; a slow consumer
/// drops oldest events rather than blocking the publisher. Process-local only —
/// horizontal scale-out is out of scope for ADR-0008.
/// </summary>
internal sealed class InMemoryRealtimeBroker : IRealtimeEventBroker
{
    private const int SubscriptionBufferSize = 256;

    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();

    public IRealtimeEventSubscription Subscribe()
    {
        var id = Guid.NewGuid();
        var subscription = new Subscription(id, this);
        _subscriptions[id] = subscription;
        return subscription;
    }

    public async ValueTask PublishAsync(RealtimeEventEnvelope envelope, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        foreach (var subscription in _subscriptions.Values)
        {
            await subscription.WriteAsync(envelope, ct);
        }
    }

    private void Remove(Guid id) => _subscriptions.TryRemove(id, out _);

    private sealed class Subscription(Guid id, InMemoryRealtimeBroker owner) : IRealtimeEventSubscription
    {
        private readonly Channel<RealtimeEventEnvelope> _channel = Channel.CreateBounded<RealtimeEventEnvelope>(
            new BoundedChannelOptions(SubscriptionBufferSize)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        private int _disposed;

        public async IAsyncEnumerable<RealtimeEventEnvelope> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                while (_channel.Reader.TryRead(out var envelope))
                {
                    yield return envelope;
                }
            }
        }

        public async ValueTask WriteAsync(RealtimeEventEnvelope envelope, CancellationToken ct)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            await _channel.Writer.WriteAsync(envelope, ct);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _channel.Writer.TryComplete();
            owner.Remove(id);
        }
    }
}
