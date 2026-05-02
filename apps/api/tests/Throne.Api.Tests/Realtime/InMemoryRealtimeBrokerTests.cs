using FluentAssertions;
using Throne.Api.Realtime;
using Throne.Realtime.Contracts;

namespace Throne.Api.Tests.Realtime;

public class InMemoryRealtimeBrokerTests
{
    [Fact(DisplayName = "Подписчик получает событие, опубликованное после подписки")]
    public async Task Subscriber_receives_published_event()
    {
        var broker = new InMemoryRealtimeBroker();
        using var subscription = broker.Subscribe();

        await broker.PublishAsync(new RealtimeEventEnvelope("intent.created", new { id = "x" }), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = subscription.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        var moved = await enumerator.MoveNextAsync();
        moved.Should().BeTrue();
        enumerator.Current.Name.Should().Be("intent.created");
    }

    [Fact(DisplayName = "Без активных подписчиков publish не падает")]
    public async Task Publish_without_subscribers_is_noop()
    {
        var broker = new InMemoryRealtimeBroker();
        var act = async () => await broker.PublishAsync(
            new RealtimeEventEnvelope("intent.deleted", new { intent_id = "x" }),
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "Каждая подписка получает свою копию события")]
    public async Task Each_subscriber_gets_its_own_copy()
    {
        var broker = new InMemoryRealtimeBroker();
        using var sub1 = broker.Subscribe();
        using var sub2 = broker.Subscribe();

        await broker.PublishAsync(new RealtimeEventEnvelope("intent.deleted", new { intent_id = "x" }), CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var first = await sub1.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token).MoveNextAsync();
        var second = await sub2.ReadAllAsync(cts.Token).GetAsyncEnumerator(cts.Token).MoveNextAsync();
        first.Should().BeTrue();
        second.Should().BeTrue();
    }
}
