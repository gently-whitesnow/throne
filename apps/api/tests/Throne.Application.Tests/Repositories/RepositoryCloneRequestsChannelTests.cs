using FluentAssertions;
using Throne.Application.Repositories;
using Throne.Domain.Repositories;

namespace Throne.Application.Tests.Repositories;

public class RepositoryCloneRequestsChannelTests
{
    [Fact(DisplayName = "EnqueueAsync пушит binding'и в порядке очереди, ReadAllAsync их выдаёт")]
    public async Task Enqueue_and_read_round_trip_preserves_order()
    {
        var channel = new RepositoryCloneRequestsChannel();
        var ids = new[] { BindingId.New(), BindingId.New(), BindingId.New() };

        foreach (var id in ids)
        {
            await channel.EnqueueAsync(id, CancellationToken.None);
        }

        var observed = new List<BindingId>();
        await foreach (var id in channel.ReadAllAsync(CancellationToken.None))
        {
            observed.Add(id);
            if (observed.Count == ids.Length)
            {
                break;
            }
        }

        observed.Should().Equal(ids);
    }

    [Fact(DisplayName = "ReadAllAsync ждёт следующий enqueue без busy-loop")]
    public async Task ReadAllAsync_waits_for_next_enqueue()
    {
        var channel = new RepositoryCloneRequestsChannel();
        var id = BindingId.New();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var consumed = new TaskCompletionSource<BindingId>();
        var consumer = Task.Run(async () =>
        {
            await foreach (var bindingId in channel.ReadAllAsync(cts.Token))
            {
                consumed.TrySetResult(bindingId);
                return;
            }
        }, cts.Token);

        await channel.EnqueueAsync(id, CancellationToken.None);
        var observed = await consumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await consumer.WaitAsync(TimeSpan.FromSeconds(5));

        observed.Should().Be(id);
    }
}
