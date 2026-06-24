using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Throne.Api.Terminals;
using Throne.Application.Terminals;

namespace Throne.Api.Tests.Terminals;

public class TerminalHookConsumerServiceTests
{
    [Fact(DisplayName = "Ошибка одного subscriber не мешает остальным и следующим событиям")]
    public async Task Subscriber_failure_is_isolated()
    {
        var bus = new InMemoryTerminalHookBus();
        var recorder = new RecordingSubscriber();
        using var service = new TerminalHookConsumerService(
            bus,
            [new ThrowingSubscriber(), recorder],
            NullLogger<TerminalHookConsumerService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await bus.PublishAsync(Hook("intent-1"), CancellationToken.None);
        await bus.PublishAsync(Hook("intent-2"), CancellationToken.None);

        await recorder.WaitForCountAsync(2);
        await service.StopAsync(CancellationToken.None);

        recorder.IntentIds.Should().Equal("intent-1", "intent-2");
    }

    private static TerminalHookEvent Hook(string intentId) =>
        new(intentId, TerminalHookEvents.Stop, TerminalRunModes.Work, DateTimeOffset.UtcNow);

    private sealed class ThrowingSubscriber : ITerminalHookSubscriber
    {
        public Task HandleAsync(TerminalHookEvent hook, CancellationToken ct) =>
            throw new InvalidOperationException("boom");
    }

    private sealed class RecordingSubscriber : ITerminalHookSubscriber
    {
        private readonly TaskCompletionSource _second =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> IntentIds { get; } = [];

        public Task HandleAsync(TerminalHookEvent hook, CancellationToken ct)
        {
            IntentIds.Add(hook.IntentId);
            if (IntentIds.Count >= 2)
            {
                _second.TrySetResult();
            }

            return Task.CompletedTask;
        }

        public async Task WaitForCountAsync(int count)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _second.Task.WaitAsync(cts.Token);
            IntentIds.Should().HaveCount(count);
        }
    }
}
