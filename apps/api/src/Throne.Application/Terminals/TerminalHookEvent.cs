namespace Throne.Application.Terminals;

public sealed record TerminalHookEvent(
    string IntentId,
    string Event,
    string? Mode,
    DateTimeOffset ReceivedAt);

public interface ITerminalHookBus
{
    ValueTask PublishAsync(TerminalHookEvent hook, CancellationToken ct);
}

public interface ITerminalHookEventReader
{
    IAsyncEnumerable<TerminalHookEvent> ReadAllAsync(CancellationToken ct);
}

public interface ITerminalHookSubscriber
{
    Task HandleAsync(TerminalHookEvent hook, CancellationToken ct);
}
