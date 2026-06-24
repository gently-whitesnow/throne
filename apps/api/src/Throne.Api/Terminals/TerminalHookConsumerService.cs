using Throne.Application.Terminals;

namespace Throne.Api.Terminals;

internal sealed partial class TerminalHookConsumerService(
    ITerminalHookEventReader reader,
    IEnumerable<ITerminalHookSubscriber> subscribers,
    ILogger<TerminalHookConsumerService> logger) : BackgroundService
{
    private readonly ITerminalHookSubscriber[] _subscribers = subscribers.ToArray();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var hook in reader.ReadAllAsync(stoppingToken))
        {
            await DispatchAsync(hook, stoppingToken);
        }
    }

    private async Task DispatchAsync(TerminalHookEvent hook, CancellationToken ct)
    {
        var deliveries = _subscribers.Select(subscriber => DeliverAsync(subscriber, hook, ct));
        await Task.WhenAll(deliveries);
    }

    private async Task DeliverAsync(ITerminalHookSubscriber subscriber, TerminalHookEvent hook, CancellationToken ct)
    {
        try
        {
            await subscriber.HandleAsync(hook, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogSubscriberFailed(logger, subscriber.GetType().Name, hook.IntentId, hook.Event, ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Error,
        Message = "Terminal hook subscriber {Subscriber} failed for intent={IntentId}, event={Event}.")]
    private static partial void LogSubscriberFailed(
        ILogger logger,
        string subscriber,
        string intentId,
        string @event,
        Exception exception);
}
