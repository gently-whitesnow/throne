using Throne.Application.Events;
using Throne.Application.Ports;
using Throne.Realtime.Contracts;

namespace Throne.Api.Realtime;

/// <summary>
/// Единственный sink для конверсии доменных событий в realtime SSE-конверты.
///
/// Добавление нового realtime-события:
///   1. добавить запись в specs/contracts/realtime/events.yaml,
///   2. добавить соответствующий <see cref="IDomainEvent"/> в Throne.Application.Events,
///   3. добавить кейс в соответствующий *RealtimeMapper (по агрегату),
///   4. поднять событие из подходящего исхода репозитория,
///   5. подписаться через useRealtimeEvent('&lt;name&gt;') на фронте.
///
/// Realtime quality gate (scripts/quality/realtime-verify-coverage.sh)
/// падает, пока не реализованы все пять шагов. Добавление нового события не
/// меняет этот dispatcher — только нужный per-aggregate mapper.
/// </summary>
internal sealed class RealtimeDomainEventHandler(
    IRealtimeEventBroker broker,
    IntentLifecycleRealtimeMapper intentLifecycle) : IDomainEventHandler
{
    public async Task HandleAsync(IDomainEvent evt, CancellationToken ct)
    {
        var envelope = await ToEnvelopeAsync(evt, ct);
        if (envelope is null)
        {
            return;
        }

        await broker.PublishAsync(envelope, ct);
    }

    private async Task<RealtimeEventEnvelope?> ToEnvelopeAsync(IDomainEvent evt, CancellationToken ct) =>
        await intentLifecycle.TryMapAsync(evt, ct) ?? StaticRealtimeMapperFanout.TryMap(evt);
}
