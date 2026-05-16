namespace Throne.Mcp.Stdio;

internal sealed class UpstreamWatcherSlot : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _watcher;

    public CancellationToken StopToken => _stop.Token;

    public void Watch(Task watcher) => _watcher = watcher;

    public void Cancel() => _stop.Cancel();

    public async ValueTask DisposeAsync()
    {
        if (_watcher is not null)
        {
            try { await _watcher; } catch { /* watcher cancellation is expected */ }
        }
        _stop.Dispose();
    }
}
