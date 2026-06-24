using System.Threading.Channels;
using Throne.Application.Terminals;

namespace Throne.Api.Terminals;

internal sealed class InMemoryTerminalHookBus : ITerminalHookBus, ITerminalHookEventReader
{
    private const int BufferSize = 1024;

    private readonly Channel<TerminalHookEvent> _channel = Channel.CreateBounded<TerminalHookEvent>(
        new BoundedChannelOptions(BufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    public ValueTask PublishAsync(TerminalHookEvent hook, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(hook);
        return _channel.Writer.WriteAsync(hook, ct);
    }

    public IAsyncEnumerable<TerminalHookEvent> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
