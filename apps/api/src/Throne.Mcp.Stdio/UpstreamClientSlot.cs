using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Throne.Mcp.Stdio;

internal sealed class UpstreamClientSlot(ILogger log) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public readonly UpstreamCurrentClient Current = new(log);
    public readonly UpstreamWatcherSlot Watcher = new();

    public async ValueTask<McpClient> GetOrReconnectAsync(Func<CancellationToken, Task> reconnectAsync, CancellationToken ct)
    {
        if (Current.TryGetAlive(out var client))
        {
            return client;
        }
        await _gate.WaitAsync(ct);
        try
        {
            return await GetOrReconnectInsideGateAsync(reconnectAsync, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ForceReconnectAsync(Func<CancellationToken, Task> reconnectAsync, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await reconnectAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Watcher.Cancel();
        await Watcher.DisposeAsync();
        await Current.DisposeAsync();
        _gate.Dispose();
    }

    private async ValueTask<McpClient> GetOrReconnectInsideGateAsync(
        Func<CancellationToken, Task> reconnectAsync,
        CancellationToken ct)
    {
        if (Current.TryGetAlive(out var client))
        {
            return client;
        }
        await reconnectAsync(ct);
        return Current.Current!;
    }
}
