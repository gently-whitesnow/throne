using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamConnection : IAsyncDisposable
{
    private readonly UpstreamClientFactory _factory;
    private readonly UpstreamClientSlot _slot;
    private readonly ILogger _log;

    public readonly UpstreamInstructionsState InstructionsState;
    public readonly UpstreamToolsState ToolsState;

    public UpstreamConnection(Func<CancellationToken, Task<McpClient>> connectAsync, ILogger log)
    {
        _log = log;
        _factory = new UpstreamClientFactory(connectAsync);
        _slot = new UpstreamClientSlot(log);
        InstructionsState = new UpstreamInstructionsState(log);
        ToolsState = new UpstreamToolsState(log);
    }

    public ValueTask<McpClient> GetOrReconnectAsync(CancellationToken ct) =>
        _slot.GetOrReconnectAsync(ReconnectUnsafeAsync, ct);

    public Task ForceReconnectAsync(CancellationToken ct) =>
        _slot.ForceReconnectAsync(ReconnectUnsafeAsync, ct);

    internal async Task ReconnectUnsafeAsync(CancellationToken ct)
    {
        var fresh = await _factory.ConnectAndListToolsAsync(ct);
        await _slot.Current.ReplaceAsync(fresh.Client);
        _slot.Watcher.Watch(UpstreamCompletionWatcher.RunAsync(this, fresh.Client, _log, _slot.Watcher.StopToken));
        InstructionsState.Update(fresh.Client.ServerInstructions);
        ToolsState.Update(fresh.Tools.Select(t => t.ProtocolTool).ToArray());
    }

    internal bool IsCurrent(McpClient client) => _slot.Current.IsCurrent(client);

    public ValueTask DisposeAsync() => _slot.DisposeAsync();
}
